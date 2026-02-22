Option Explicit

Dim wshShell, fso
Set wshShell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")

Dim trollPath
trollPath = "C:\Users\Kiwi\Desktop\Project 11\Trollware\TrollUI\bin\Debug\net10.0-windows\TrollUI.exe"

If fso.FileExists(trollPath) Then
    wshShell.Run """" & trollPath & """", 0, False
End If

Set fso = Nothing
Set wshShell = Nothing
