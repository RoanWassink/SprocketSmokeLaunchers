<!-- sp-compat {"hamish.sprocket": "0.2.55.5", "bepinex.bepinex": "6.0.0-be.788"} -->

## Smoke Launchers 0.2.5 — Beta

More launchers, one salvo! This update fixes a limit that prevented vehicles with nine or more loaded smoke banks from firing.

- Fire up to **16 banks / 48 grenades** together, provided enough smoke-effect slots are free. Each bank still fires three grenades.
- Clear warnings explain too many banks, smoke effects still initializing, or insufficient free slots.
- Rejected volleys do **not** spend ammunition. The smoke pool size is unchanged; existing effects may need to clear before another vehicle can fire.
- Existing launch/ignition sounds, mirroring, controls, vehicle saves and configuration remain compatible.

**Required: [Keybinds API 0.1.5](https://github.com/RoanWassink/SprocketKeybinds/releases/tag/v0.1.5). Without it this plugin will not load.** Also requires Sprocket 0.2.55.5 on Windows x64 and a working Sprocket Mod Loader / BepInEx 6 IL2CPP 6.0.0-be.788 setup. Neither dependency is bundled. For Cold War availability, install the [Core or full pack](https://github.com/RoanWassink/SprocketColdWarExpansionPack/releases/tag/v0.1.0).

**Install/update:** close the game and back up your saves and matching mod files. Extract the ZIP's BepInEx and Sprocket_Data folders into the folder containing Sprocket.exe. Replace matching files, retain just one Smoke DLL, and preserve your configs and keybinds. Full-pack users can update Smoke this way; the v0.1.0 pack ZIP itself still contains 0.2.4. Restore backed-up files to roll back.

**Beta limits:** automated capacity and geometry checks passed, but the new 9/16/17-bank and busy-pool scenarios still need in-game confirmation. Smoke is visual only and does not block AI or thermal vision. Back up vehicle saves before testing.

[Full installation, controls and troubleshooting](https://github.com/RoanWassink/SprocketSmokeLaunchers#readme).

[Support my ChatGPT budget and help me reverse engineer Sprocket to make more mods](https://www.paypal.com/donate/?hosted_button_id=7PE3SDBETXFQ6).