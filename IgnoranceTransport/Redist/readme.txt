This folder (Redist) contains a compiled DLL for x86_64 Windows, MacOS and Linux distros.
x86 Windows target might come at a later date, depends if ENET will compile for non-64bit
targets.

Please note that you may need to exclude the Windows DLL on non-Windows targets. 
This can be done by selecting the enet.dll and selecting "Editor" from "Exclude from platforms"
under the OS you're running.

For example:

Windows: Select both enet.bundle and libenet.so and choose Exclude from platforms: Editor
MacOS: Select enet.dll and libenet.so and choose Exclude from platform: Editor
Linux: Select enet.dll and enet.bundle and choose Exclude from platform: Editor

If you get an error about a DLL not having valid meta data, make sure you have done above steps correctly.
Otherwise, restart Unity and if it persists open a support ticket.