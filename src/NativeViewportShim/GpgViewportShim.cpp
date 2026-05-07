#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <tlhelp32.h>

namespace
{
    const int kTargetWidth = 2160;
    const int kTargetHeight = 3840;

    using GetClientRectFn = BOOL(WINAPI *)(HWND, LPRECT);
    using WindowProcFn = LRESULT(CALLBACK *)(HWND, UINT, WPARAM, LPARAM);

    GetClientRectFn g_realGetClientRect = nullptr;
    void **g_getClientRectSlots[128] = {};
    int g_getClientRectSlotCount = 0;

    struct SubclassEntry
    {
        HWND hwnd;
        WNDPROC originalProc;
    };

    SubclassEntry g_subclassedWindows[16] = {};
    int g_subclassedWindowCount = 0;

    void InstallWindowSubclass();

    bool ContainsText(const wchar_t *haystack, const wchar_t *needle)
    {
        if (haystack == nullptr || needle == nullptr)
        {
            return false;
        }

        return wcsstr(haystack, needle) != nullptr;
    }

    bool IsTargetGameChildWindow(HWND hwnd)
    {
        if (hwnd == nullptr || g_realGetClientRect == nullptr)
        {
            return false;
        }

        HWND parent = GetParent(hwnd);
        if (parent == nullptr)
        {
            return false;
        }

        wchar_t parentTitle[256] = {};
        GetWindowTextW(parent, parentTitle, ARRAYSIZE(parentTitle));
        if (!ContainsText(parentTitle, L"Whiteout Survival"))
        {
            return false;
        }

        wchar_t childTitle[64] = {};
        GetWindowTextW(hwnd, childTitle, ARRAYSIZE(childTitle));
        if (!ContainsText(childTitle, L"crosvm"))
        {
            return false;
        }

        RECT actual = {};
        if (!g_realGetClientRect(hwnd, &actual))
        {
            return false;
        }

        const int width = actual.right - actual.left;
        const int height = actual.bottom - actual.top;
        return width > 0 && height > width;
    }

    BOOL WINAPI HookedGetClientRect(HWND hwnd, LPRECT rect)
    {
        BOOL ok = g_realGetClientRect(hwnd, rect);
        if (ok && rect != nullptr && IsTargetGameChildWindow(hwnd))
        {
            rect->left = 0;
            rect->top = 0;
            rect->right = kTargetWidth;
            rect->bottom = kTargetHeight;
        }

        return ok;
    }

    WNDPROC FindOriginalWindowProc(HWND hwnd)
    {
        for (int i = 0; i < g_subclassedWindowCount; i++)
        {
            if (g_subclassedWindows[i].hwnd == hwnd)
            {
                return g_subclassedWindows[i].originalProc;
            }
        }

        return nullptr;
    }

    LRESULT CALLBACK HookedWindowProc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam)
    {
        WNDPROC originalProc = FindOriginalWindowProc(hwnd);
        if (originalProc == nullptr)
        {
            return DefWindowProcW(hwnd, message, wParam, lParam);
        }

        if (message == WM_SIZE)
        {
            LPARAM fakeSize = MAKELPARAM(kTargetWidth, kTargetHeight);
            return CallWindowProcW(originalProc, hwnd, message, wParam, fakeSize);
        }

        if (message == WM_WINDOWPOSCHANGED || message == WM_WINDOWPOSCHANGING)
        {
            WINDOWPOS fakeWindowPos = {};
            WINDOWPOS *windowPos = reinterpret_cast<WINDOWPOS *>(lParam);
            if (windowPos != nullptr)
            {
                fakeWindowPos = *windowPos;
                fakeWindowPos.cx = kTargetWidth;
                fakeWindowPos.cy = kTargetHeight;
                return CallWindowProcW(originalProc, hwnd, message, wParam, reinterpret_cast<LPARAM>(&fakeWindowPos));
            }
        }

        return CallWindowProcW(originalProc, hwnd, message, wParam, lParam);
    }

    bool PatchImport(HMODULE module, const char *dllName, const char *functionName, void *replacement, void ***slot, void **original)
    {
        if (module == nullptr)
        {
            return false;
        }

        auto *base = reinterpret_cast<unsigned char *>(module);
        auto *dosHeader = reinterpret_cast<IMAGE_DOS_HEADER *>(base);
        if (dosHeader->e_magic != IMAGE_DOS_SIGNATURE)
        {
            return false;
        }

        auto *ntHeaders = reinterpret_cast<IMAGE_NT_HEADERS *>(base + dosHeader->e_lfanew);
        if (ntHeaders->Signature != IMAGE_NT_SIGNATURE)
        {
            return false;
        }

        IMAGE_DATA_DIRECTORY importDirectory =
            ntHeaders->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
        if (importDirectory.VirtualAddress == 0)
        {
            return false;
        }

        auto *descriptor = reinterpret_cast<IMAGE_IMPORT_DESCRIPTOR *>(base + importDirectory.VirtualAddress);
        for (; descriptor->Name != 0; descriptor++)
        {
            const char *currentDllName = reinterpret_cast<const char *>(base + descriptor->Name);
            if (_stricmp(currentDllName, dllName) != 0)
            {
                continue;
            }

            auto *originalThunk = reinterpret_cast<IMAGE_THUNK_DATA *>(base + descriptor->OriginalFirstThunk);
            auto *firstThunk = reinterpret_cast<IMAGE_THUNK_DATA *>(base + descriptor->FirstThunk);
            for (; originalThunk->u1.AddressOfData != 0; originalThunk++, firstThunk++)
            {
                if (IMAGE_SNAP_BY_ORDINAL(originalThunk->u1.Ordinal))
                {
                    continue;
                }

                auto *importByName = reinterpret_cast<IMAGE_IMPORT_BY_NAME *>(base + originalThunk->u1.AddressOfData);
                if (strcmp(reinterpret_cast<const char *>(importByName->Name), functionName) != 0)
                {
                    continue;
                }

                DWORD oldProtect = 0;
                void **functionSlot = reinterpret_cast<void **>(&firstThunk->u1.Function);
                if (!VirtualProtect(functionSlot, sizeof(void *), PAGE_READWRITE, &oldProtect))
                {
                    return false;
                }

                if (*functionSlot == replacement)
                {
                    return false;
                }

                *original = *functionSlot;
                *functionSlot = replacement;
                VirtualProtect(functionSlot, sizeof(void *), oldProtect, &oldProtect);
                FlushInstructionCache(GetCurrentProcess(), functionSlot, sizeof(void *));
                *slot = functionSlot;
                return true;
            }
        }

        return false;
    }

    void TryPatchModule(HMODULE module, int *patchedCount)
    {
        void *original = nullptr;
        void **slot = nullptr;
        if (!PatchImport(module, "USER32.dll", "GetClientRect", reinterpret_cast<void *>(&HookedGetClientRect), &slot, &original))
        {
            return;
        }

        if (g_realGetClientRect == nullptr)
        {
            g_realGetClientRect = reinterpret_cast<GetClientRectFn>(original);
        }

        if (g_getClientRectSlotCount < ARRAYSIZE(g_getClientRectSlots))
        {
            g_getClientRectSlots[g_getClientRectSlotCount] = slot;
            g_getClientRectSlotCount++;
        }

        if (patchedCount != nullptr)
        {
            (*patchedCount)++;
        }
    }

    void InstallHook()
    {
        int patchedCount = 0;
        TryPatchModule(GetModuleHandleW(nullptr), &patchedCount);

        HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE, GetCurrentProcessId());
        if (snapshot != INVALID_HANDLE_VALUE)
        {
            MODULEENTRY32W entry = {};
            entry.dwSize = sizeof(entry);
            if (Module32FirstW(snapshot, &entry))
            {
                do
                {
                    TryPatchModule(entry.hModule, &patchedCount);
                }
                while (Module32NextW(snapshot, &entry));
            }

            CloseHandle(snapshot);
        }

        wchar_t message[128] = {};
        wsprintfW(message, L"GpgViewportShim: patched GetClientRect imports: %d.\n", patchedCount);
        OutputDebugStringW(message);

        if (patchedCount == 0)
        {
            OutputDebugStringW(L"GpgViewportShim: failed to install GetClientRect hook.\n");
        }

        InstallWindowSubclass();
    }

    void SubclassChildWindow(HWND child)
    {
        if (g_subclassedWindowCount >= ARRAYSIZE(g_subclassedWindows))
        {
            return;
        }

        for (int i = 0; i < g_subclassedWindowCount; i++)
        {
            if (g_subclassedWindows[i].hwnd == child)
            {
                return;
            }
        }

        SetLastError(0);
        LONG_PTR previous = SetWindowLongPtrW(child, GWLP_WNDPROC, reinterpret_cast<LONG_PTR>(&HookedWindowProc));
        if (previous == 0 && GetLastError() != 0)
        {
            OutputDebugStringW(L"GpgViewportShim: failed to subclass child window.\n");
            return;
        }

        g_subclassedWindows[g_subclassedWindowCount].hwnd = child;
        g_subclassedWindows[g_subclassedWindowCount].originalProc = reinterpret_cast<WNDPROC>(previous);
        g_subclassedWindowCount++;
        OutputDebugStringW(L"GpgViewportShim: subclassed Whiteout child window.\n");
    }

    BOOL CALLBACK EnumChildProc(HWND hwnd, LPARAM)
    {
        if (IsTargetGameChildWindow(hwnd))
        {
            SubclassChildWindow(hwnd);
        }

        return TRUE;
    }

    BOOL CALLBACK EnumParentProc(HWND hwnd, LPARAM)
    {
        DWORD processId = 0;
        GetWindowThreadProcessId(hwnd, &processId);
        if (processId != GetCurrentProcessId())
        {
            return TRUE;
        }

        wchar_t parentTitle[256] = {};
        GetWindowTextW(hwnd, parentTitle, ARRAYSIZE(parentTitle));
        if (ContainsText(parentTitle, L"Whiteout Survival"))
        {
            EnumChildWindows(hwnd, &EnumChildProc, 0);
        }

        return TRUE;
    }

    void InstallWindowSubclass()
    {
        EnumWindows(&EnumParentProc, 0);
    }

    void RemoveHook()
    {
        for (int i = 0; i < g_subclassedWindowCount; i++)
        {
            if (g_subclassedWindows[i].hwnd != nullptr && g_subclassedWindows[i].originalProc != nullptr)
            {
                SetWindowLongPtrW(
                    g_subclassedWindows[i].hwnd,
                    GWLP_WNDPROC,
                    reinterpret_cast<LONG_PTR>(g_subclassedWindows[i].originalProc));
            }
        }

        if (g_realGetClientRect == nullptr)
        {
            return;
        }

        for (int i = 0; i < g_getClientRectSlotCount; i++)
        {
            DWORD oldProtect = 0;
            if (g_getClientRectSlots[i] != nullptr
                && VirtualProtect(g_getClientRectSlots[i], sizeof(void *), PAGE_READWRITE, &oldProtect))
            {
                *g_getClientRectSlots[i] = reinterpret_cast<void *>(g_realGetClientRect);
                VirtualProtect(g_getClientRectSlots[i], sizeof(void *), oldProtect, &oldProtect);
                FlushInstructionCache(GetCurrentProcess(), g_getClientRectSlots[i], sizeof(void *));
            }
        }
    }
}

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(instance);
        InstallHook();
    }
    else if (reason == DLL_PROCESS_DETACH)
    {
        RemoveHook();
    }

    return TRUE;
}
