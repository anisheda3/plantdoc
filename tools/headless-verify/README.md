# Headless Verification Harness (retired)

This harness compiled the runtime scripts against a hand-written `UnityStub.cs` so logic
could be tested without Unity. It does **not** compile any more: the October 2026 rework
added UI, camera and AR code that uses Unity types the stub never modelled (CanvasGroup,
TextMeshPro input fields, RenderTexture capture, AR Foundation…), and keeping a fake
UnityEngine in sync is not worth it now that the real editor runs headlessly.

Use the real Unity test runner instead — same tests, real engine, no Editor window:

```powershell
powershell -ExecutionPolicy Bypass -File tools\run-tests.ps1           # EditMode + end-to-end PlayMode
powershell -ExecutionPolicy Bypass -File tools\run-tests.ps1 -Screens  # also refresh docs/screenshots
```

The folder is kept only for history; it is safe to delete.
