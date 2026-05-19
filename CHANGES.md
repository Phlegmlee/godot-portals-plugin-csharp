# Changelog
All notable changes to the C# version of [godot-portals-plugin-csharp] will be documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.9.5] - 2026-05-19
### Fixed
- Updated boolean logic in getters responsible for the portals children nodes to account for empty string paths and not just null.

## [0.9.4] - 2026-05-19
### Added
- Gizmos UI integration. Portal outline and forward direction gizmos created.

## [0.8.4] - 2026-05-19
### Fixed
- .NET Assembly Unloading Error [Issue #7]
- Export property persistence problem [Issue #4 and #12]

## [0.8.3] - 2026-05-17
### Added
- Third person camera smooth teleport support.

## [0.8.2] - 2026-05-17
### Fixed
- Metadata on portal-to-self tranfer is now maintained. [Issue #8]
- Updating smooth teleport mesh clones on metadata transfer to remove visual stuttering. [Issue #9]

## [0.8.1] - 2026-05-15
### Fixed
- Smooth teleport clipping material updated to remove ghosting. [Issue #3]
- Duplicate meshes bug. [Issue #5]
- Start deactivated was missing its argument.

## [0.8.0] - 2026-05-15
### Added
- C# rewrite of main plugin completed.
