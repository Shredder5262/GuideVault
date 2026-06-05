# Guidevault metadata classification notes

Guidevault 0.9.28 tightens filename and folder inference after a false-positive where `Primal Rage` matched the publisher token `Prima` and was classified as a strategy guide.

Classification keywords now use whole-word/phrase matching for guide, magazine, and publisher detection. This keeps fast indexing lightweight while avoiding accidental substring matches inside game titles.

If an item is still placed in the wrong type, check the library type first. A library explicitly set to `Manuals`, `Strategy Guides`, or `Magazines` takes precedence over mixed-library filename inference.
