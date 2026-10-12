# Mobile gameplay rework task tracker

Branch: `feat/mobile-gameplay-rework`

## Input and camera

- [x] Keep joystick ownership by `pointerId`.
- [x] Exclude the joystick rectangle and selectable UI from camera look input.
- [x] Reset look/joystick state on pointer cancel, drag end, pause, focus loss and disable.
- [x] Preserve drag delta accumulated before the uGUI threshold.
- [x] Smooth camera rotation and clamp pitch.
- [x] Add editor coverage for exclusion, raycast filtering and multi-touch cancellation.

## Energy and hazards

- [x] Select a random starting supply in `target + 2..5`.
- [x] Raise runtime capacity to the maximum possible supply.
- [x] Keep the existing walkable/path/spacing solver and risk sectors.
- [x] Mark candidate positions near existing energy cores as trap-risk positions.
- [x] Delay initial harmful placement by one frame so it can use real core positions.
- [x] Keep the fallback respawn rule that guarantees enough net energy to finish.
- [x] Update level assets and editor/play-mode expectations for the new range.

## Enemies and boss

- [x] Make newly spawned creep/boss agents chase the player from any distance.
- [x] Preserve repath/stuck recovery and difficulty-scaled speed/damage profiles.
- [x] Mark the final level boss as lethal on contact, bypassing shield and extra lives.
- [x] Add a final-level intro page that states the contact-kill rule.

## Guide and visual language

- [x] Add `StoryPage.illustrationKey` and runtime-loaded guide images.
- [x] Add guide pages for creep/boss, energy, healing/shield, harmful items and overdrive.
- [x] Add a final boss page with a distinct boss icon.
- [x] Add explicit HP plus/minus and slow-status icons for the guide vocabulary.
- [x] Add tests that every referenced guide image resolves from `Resources`.

## Verification

- [x] Unity 2020.3 script compilation succeeds with no compiler errors.
- [x] `git diff --check` passes.
- [ ] Run EditMode/PlayMode test reports on a Unity installation with a working Test Runner invocation.
- [ ] Device playtest: touch camera + joystick simultaneously on 16:9 and 20:9 screens.
- [ ] 1000-seed spawn distribution and win-path report.
