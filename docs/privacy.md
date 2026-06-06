# Public Release and Privacy Checklist

This repository is prepared as a minimal public baseline. Before pushing it to a public remote, check the following items.

## Published

- Minimal Pico 2 wiring documentation
- Minimal Pico SDK ranging firmware
- Third-party VL53L1X C API with upstream license preserved
- Public build instructions
- AGPL-3.0 license

## Not Published

- Personal notes or local TODO files
- Private calibration values
- Private entropy generation or conditioning algorithms
- Product-specific thresholds or unreleased decision logic
- Device serial numbers or inventory records
- Wi-Fi SSIDs, passwords, API keys, tokens, or server URLs
- Raw captures, logs, datasets, or generated binaries
- Proprietary board files or supplier documents

## Before Push

Run:

```powershell
git status --short
rg -n "password|passwd|secret|token|api[_-]?key|ssid|wifi|private|credential|bearer|http://|https://" .
```

Review every match manually. Public documentation may mention these words as examples, but no real value should be present.
