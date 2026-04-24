> [!WARNING]
> This is in early stages of development. You can and will run into problems.

<center><img width="1277" height="1393" alt="image" src="https://github.com/user-attachments/assets/ea9351ca-a9c4-4649-92e8-30a800b09907" />
</center>


# Aemulus Cross-Platform Mod Manager (Pending namechange to Banana-Peel)
Tekka's [Aemulus Mod Manager](https://github.com/TekkaGB/AemulusModManager) ported to Avalonia and Dotnet 8. For the bulk of the documentation, I recommend you take a peek there, as I will only cover the important bits (for now until the next major refactor).

# Dependencies
## Build
### AemulusCPMM
All build dependencies in ThirdParty.
`dotnet-sdk-8.0`

### ThirdParty
deceboot: `gcc g++`

awbtools: `gcc`

Atlus-Script-Tools: `dotnet-sdk-8.0`

AtlusFileSystemLibrary: `dotnet-sdk-8.0`


## Runtime
Linux: `7z mono` (Both of these will be phased out eventually)

Windows: A not broken OS, idk.


# Bug Testing
Welcome partner, you're in for a wild ride of testing this software. I don't have the time and energy to test every single use-case so I am counting on you to report issues to me along with the steps to reproduce them so I can take a look and get it fixed. 

# Known Issues
* Drag and drop doesn't function due to limitations of Avalonia under Wayland. Either run the application under xorg, or use the add package button.
* No CPK support under Linux (currently)
* There may be a few other build time dependencies missing for other games. They will be addressed in time.

# Installation
I don't want to deal with packaging, so if you want to run an AUR package or something let me know.

You can download a windows or linux build from [Releases](https://github.com/alexankitty/AemulusCPMM/releases)

Linux builds are in both binary and appimage format

# Long Term
* Create a plugin system to support games arbitrarily???