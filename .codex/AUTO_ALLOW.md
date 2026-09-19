# ShipSim159 approved check commands

User-requested auto-allow setup, 2026-09-20. WSL rules:
`/home/askibin/.codex/rules/shipsim159.rules`. Restart Codex to load new rule files.

Run from WSL without a heredoc, redirection, or an enclosing `zsh -lc` command:

```sh
powershell.exe -NoProfile -File 'G:\Projects\ShipSim159\.codex\tools\shipsim-check.ps1' -Action Status
powershell.exe -NoProfile -File 'G:\Projects\ShipSim159\.codex\tools\shipsim-check.ps1' -Action EditMode
python3 -I /mnt/g/projects/shipsim159/.codex/tools/read-test-results.py
```

Actions: Status, Compile, EditMode, PlayMode, Shakedown, FastCrafts, BuildCatalogue,
PhaseOne, WaterWeather, Wake, Buoys, Menu. No arbitrary command, method, project, log path,
process ID or script input is accepted. Existing Unity processes for this project prevent
another launch. Logs and XML results receive timestamped names. BuildCatalogue regenerates
vessel assets; runtime checks can overwrite their documented screenshot folders.
Unity actions execute the trusted project's C# code, so this is a bounded workflow allowance,
not an isolation boundary against malicious project code. Review changes to these launchers.

Python heredocs that modify source/docs, arbitrary Python/PowerShell execution, process
termination and destructive Git commands were not added to auto-allow. Read-only sandbox
file writes still need approval. Existing user rules were preserved, including the already
broad `powershell.exe -NoProfile -Command` allowance; these new rules do not narrow it.

Audit: `/home/askibin/.codex/shipsim159-approval-audit.json` lists recovered escalation
requests, purposes and hashes. It is not proof of individual UI approval clicks.
Official rules documentation: https://learn.chatgpt.com/docs/agent-configuration/rules
