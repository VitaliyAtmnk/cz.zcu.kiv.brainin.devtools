# BrainIn Development Tools

Unity package containing editor validation and diagnostic tools for BrainIn WebGL tasks.

The package is intended to support development of Unity tasks built on top of the current BrainIn template. It checks project setup, BrainIn integration, game contracts, localization files, scene prerequisites and runtime result output.

## Requirements

* Unity `6000.0` or newer
* BrainIn template package `cz.zcu.kiv.fav`
* Newtonsoft JSON package available in the Unity project

## Installation

TODO:

## Validation tools

Open the validation window from:

```text
BrainIn > DevTools > Validation Report
```

Then click:

```text
Run Validation
```

The validation window runs the default validation rule set and displays errors, warnings and informational results. Results can also be exported as a JSON report.

Currently implemented validation areas:

* WebGL and WASM compatibility scan
* WebGL build readiness
* BrainIn template integration
* BrainIn game contract validation
* localization file validation
* missing scripts and broken references
* required serialized references
* scene runtime prerequisites, such as `EventSystem`, input module and camera setup

### Required references

Game-specific serialized references can be marked as required using:

```csharp
using BrainIn.DevTools.Validation; // required namespace
using UnityEngine;
using UnityEngine.UI;

public sealed class ExampleController : MonoBehaviour
{
    [SerializeField, RequiredReference] // attribute RequiredReference
    private Button startButton;
}
```

The validation tool reports an error when a field marked with `RequiredReference` is not assigned.

## Result diagnostics

Open the result diagnostics window from:

```text
BrainIn > DevTools > Result Diagnostics
```

This tool analyzes BrainIn `ToWeb_GameFinished` output. It can either analyze pasted JSON/log text or capture the output automatically from the Unity Console while the game is running in Play Mode.

Recommended workflow:

```text
1. Open Result Diagnostics.
2. Run the task in Play Mode.
3. Click Start Capture.
4. Finish the task.
5. The tool captures ToWeb_GameFinished automatically.
6. Review findings.
7. Export JSON Report if needed.
```

The diagnostics check:

* whether the result JSON can be parsed
* top-level result fields
* round count consistency
* negative or invalid time values
* timeout consistency
* click ID and selected answer action ID consistency
* `isCorrect` and `successfully` consistency
* expected `customData` keys, when declared

## Expected customData contract

The BrainIn template does not define a unified declarative format for custom output values. The package therefore provides an optional attribute for declaring expected `customData` keys.

```csharp
using BrainIn.DevTools.Diagnostics;
using BrainInTemplate.Runtime.Code.Unity.Controller.Game;

public sealed class ExampleGameController : CustomGameController
{
    [ExpectedCustomDataKey]
    private bool _isCorrect;

    [ExpectedCustomDataKey("reactionTimeSeconds")]
    private double _reactionTime;
}
```

When no key is provided, the key is derived from the field or property name:

```text
_isCorrect -> isCorrect
_reactionTimeSeconds -> reactionTimeSeconds
```

In the Result Diagnostics window, click:

```text
Load From Open Scene
```

The tool scans open editor contexts, loads declared expected keys and verifies that each analyzed round contains them in `customData`.

## Batchmode validation

Validation can also be executed from command line:

```text
Unity -batchmode
  -projectPath "path/to/project"
  -executeMethod BrainIn.DevTools.Editor.CI.Batchmode.Run
  -braininValidationReportPath "path/to/report.json"
```

The process exits with code `1` when validation errors are found, otherwise with code `0`.