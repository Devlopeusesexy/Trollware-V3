Option Explicit

Dim wshShell
Set wshShell = CreateObject("WScript.Shell")
wshShell.Run """C:\Users\Kiwi\Desktop\Project 11\Trollware\TrollUI\bin\Debug\net10.0-windows\TrollUI.exe""", 0, False
Set wshShell = Nothing
