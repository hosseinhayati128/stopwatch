# Modular frame preview without stretching

Native design revision, 2026-11-05. User correction: "no stretching please". The earlier nine-slice comparison in `../native-frame-preview/` is retained as the rejected baseline.

## Result

`same-frame-three-sizes.png` shows the same existing Engraved Filigree artwork as a 420 x 132 stopwatch frame, a 460 x 480 picker frame, and an 820 x 650 larger-panel frame. This is an actual Avalonia-rendered design prototype, not generated artwork or an installed app change. All panel contents are static examples.

Each source piece is drawn at one uniform scale of 0.18 on both axes. The full corner ornaments and center side ornaments keep their size and proportions. Straight border sections repeat; a clip trims the final segment instead of resizing it. Vertical rails rotate a short horizontal source segment by 90 degrees without changing its proportions.

The renderer reads source rectangles directly from the existing `engraved-filigree.png`. It creates no cropped source PNGs or new bundled assets. The source SHA-256 is unchanged. The four review images live only under `design/`.

## Implementation and verification

- Prototype: `tools/DesktopRegressionChecks/ModularFiligreePreview.cs`.
- Every drawing call asserts that both calculated scale factors equal 0.18. Any anisotropic or size-dependent scaling throws.
- The three frame dimensions use 76, 142 and 236 placements respectively, including repeated rail segments and six fixed ornaments.
- `verification.json` records every source rectangle, destination rectangle, rotation, clip and the source hash.
- Native preview completed on the private hidden desktop. Build completed with zero errors and warnings. The active desktop, running application and saved settings were not changed.
- The 24 pixel comparisons at 96/144/192 export DPI sample the same corner subregions as the baseline; they are diagnostics, not byte-identity assertions. The full-piece geometry checks enforce the no-stretch rule. Final review exports use 96 DPI.

Reproduce after building the harness:

```powershell
powershell -NoProfile -NonInteractive -WindowStyle Hidden -File tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode '--frame-modules-preview' -LogPath '.superpowers/sdd/2026-10-31-stopwatch-overlay/frame-modules-preview.log'
```

This revision covers one frame and the shown dimensions. Production integration, other frame styles, smaller-than-minimum panels, pixel-perfect seam checks across physical monitors, and background pattern scaling remain outside this visual spike. No performance claim is made from this screenshot fixture.
