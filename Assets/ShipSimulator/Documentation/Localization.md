# Runtime localization

The language selector is under Settings > Language in both the start and pause
menus. It offers English, Русский, Deutsch and Español. Selection is saved to
`ShipSim159.Settings.Language` immediately. English is the default and the fallback
for unsupported preferences or missing translations. Changing language neither
reloads a scene nor changes the voyage save schema.

## Ownership

`Localization` loads four UTF-8 JSON catalogues from `Resources/Localization`.
Each catalogue contains the same stable keys, format placeholders and rich-text
markup. `Get(key, arguments)` formats using the selected display culture without
changing the process culture or JSON serialization. Missing keys use English;
unknown keys appear as `[key]` and generate one editor/development warning.

`Source` is an exact canonical-English-to-key adapter for existing scene-authored
phase descriptions and UI builders. It does not translate substrings or match
formatted messages. Unknown source text is preserved for identifiers, proper names
and user-authored scenario content. Use an explicit catalogue key for new UI.
Do not pass a rendered sentence through `Source` expecting numbers or fragments
to be translated. Use a keyed format template and its arguments instead.

`LocalizedLabel` retains a source or formatting function and refreshes the existing
Text component on the language event and when a hidden panel becomes visible.
Live telemetry uses the same catalogue from its normal refresh. Settings rows
reflow in place, preserving their sliders, callbacks and keyboard selection.

`LocalizedMessage` retains an event template and arguments for transient tackle
notices. Loading state retains its canonical status code. Translations do not
control physics branches, anchor warning colours, cargo identity or save IDs.
Camera and mission names are resolved when displayed. Exceptions remain in the
technical log; the menu shows a translated action failure and recovery guidance.

## Adding text

1. Add a descriptive, stable key to all four JSON files. Keep the key when editing
   wording; the existing keys are opaque identifiers, not something to recompute.
2. Keep numeric placeholders, precision and rich-text tags equivalent. Translate
   complete sentences where word order differs.
3. Bind static text with `LocalizedLabel.Bind(label, () => Localization.Get(key))`.
   Supply current arguments for dynamic text. Do not cache a translated sentence
   as simulation state.
4. Run `LocalizationTests` and the runtime language/layout checks. Verify long
   strings in the existing scroll layout rather than shrinking the font.

The built-in `LegacyRuntime.ttf` remains the shipped font. Tests request and check
all catalogue characters, including Cyrillic, umlauts, ß and Spanish accents.
Compass letters, keyboard keys, vessel project numbers and standard SI unit
abbreviations may remain conventional international notation.

This is localization of a gameplay and engineering prototype, not validation of
its maritime terminology, navigation data or training suitability.
