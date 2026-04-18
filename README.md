> [WARNING]
> This is in early stages of development. You can and will run into problems.

# Aemulus Cross-Platform Mod Manager
Tekka's [Aemulus Mod Manager](https://github.com/TekkaGB/AemulusModManager) ported to Avalonia and Dotnet 8. For the bulk of the documentation, I recommend you take a peak there, as I will only cover the important bits.

# Bug Testing
Welcome partner, you're in for a wild ride of testing this software. I don't have the time and energy to test every single use-case so I am counting on you to report issues to me along with the steps to reproduce them so I can take a look and get it fixed. 

# Known Issues
* Flow compiler doesn't properly compile down mods that add additional files that don't exist in the unpack (Ex: Persona 3's Quick travel)
* One click install isn't implemented
* Drag and drop doesn't function due to limitations of Avalonia under Wayland. Either run the application under xorg, or use the add package button.

# Installation
I don't want to deal with packaging, so if you want to run an AUR package or something let me know.

You can download a windows or linux build from [Releases](https://github.com/alexankitty/AemulusCPMM/releases)

Linux builds are in both binary and appimage format