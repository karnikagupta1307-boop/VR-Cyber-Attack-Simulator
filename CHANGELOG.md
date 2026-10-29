# Changelog
## 0.1.0 (unbuilt source release)
- Initial project source: gameplay scripts, VR rig/pointer, UI, procedural audio, Editor generator and validator, package manifest.
- Not compiled, not run, no scenes generated in archive, no APK, no headset test.

## 0.1.1
- ZIP now has project files at the archive root (no wrapper folder).
- Generator prompts with a dialog instead of running silently; waits for compile/package import.
- Added CyberDefense > Diagnose Project and START_HERE.txt.
- Still unbuilt/untested: no Unity available to the author.

## 0.2.0
- Removed XROrigin / XR Core Utils dependency and unverifiable package pins (OpenXR, XR Management).
- Generator installs XR packages by name, resumes after domain reload, validates automatically after generation, reports failures by dialog or exit code.
- Added generate_assets.sh / .bat for headless generation.
- Still no Unity access for the author: nothing compiled, generated or run.
