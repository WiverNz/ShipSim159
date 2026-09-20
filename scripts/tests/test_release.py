"""Release flow checks using copied scripts, fixture settings and a fake Git executable."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import textwrap
import unittest


POWERSHELL = '--powershell' in sys.argv
if POWERSHELL:
    sys.argv.remove('--powershell')
SCRIPTS = Path(__file__).resolve().parents[1]
SETTINGS = 'ProjectSettings/ProjectSettings.asset'
MOCK_GIT = r'''
import json, os, pathlib, sys
args = sys.argv[1:]
with open(os.environ['RELEASE_TEST_LOG'], 'a') as log:
    log.write(json.dumps(args) + '\n')
mode = os.environ.get('RELEASE_TEST_MODE', '')
command = args[0]
if mode == command + '-fail':
    sys.exit(1)
if command == 'rev-parse':
    print('true' if mode == 'shallow' else 'false') if '--is-shallow-repository' in args else print('deadbeef')
elif command == 'symbolic-ref':
    if mode == 'detached': sys.exit(1)
    print('main')
elif command == 'status':
    if mode == 'dirty': print(' M unrelated-file')
elif command == 'show-ref':
    sys.exit(0 if mode == 'existing' else 1)
elif command == 'remote':
    if mode == 'no-remote': sys.exit(1)
    print('https://example.invalid/repo')
elif command == 'show':
    if mode == 'committed-mismatch': print('  bundleVersion: 0.0.0')
    else: sys.stdout.write(pathlib.Path('ProjectSettings/ProjectSettings.asset').read_text())
elif command in ('add', 'commit', 'tag', 'push'):
    if mode == 'branch-fail' and args == ['push', 'origin', 'main']: sys.exit(1)
else:
    sys.exit('Unexpected Git command: ' + repr(args))
'''


class ReleaseTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='shipsim release ')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        (self.root / 'scripts').mkdir()
        self.script = self.root / 'scripts' / ('release.ps1' if POWERSHELL else 'release.sh')
        shutil.copyfile(SCRIPTS / self.script.name, self.script)
        self.settings = self.root / SETTINGS
        self.settings.parent.mkdir()
        self.original = b'PlayerSettings:\n  bundleVersion: 1.2.3\n  productName: ShipSim159\n'
        self.settings.write_bytes(self.original)
        self.log = self.root / 'git-calls.jsonl'
        mock = self.root / 'mock_git.py'
        mock.write_text(MOCK_GIT)
        if os.name == 'nt':
            executable = self.root / 'git.cmd'
            executable.write_text(f'@"{sys.executable}" "{mock}" %*\n@exit /b %errorlevel%\n')
        else:
            executable = self.root / 'git'
            executable.write_text(f'#!{sys.executable}\n' + MOCK_GIT)
            executable.chmod(0o755)
        self.env = dict(os.environ, PATH=str(self.root) + os.pathsep + os.environ['PATH'],
                        RELEASE_TEST_LOG=str(self.log))

    def run_release(self, *args, mode='', answer='n\n'):
        command = (['powershell.exe', '-NoProfile', '-ExecutionPolicy', 'RemoteSigned', '-File']
                   if POWERSHELL else ['bash'])
        self.log.write_text('')
        result = subprocess.run(command + [str(self.script), *args], cwd=self.root,
                                env=dict(self.env, RELEASE_TEST_MODE=mode), input=answer,
                                capture_output=True, text=True)
        self.calls = [json.loads(line) for line in self.log.read_text().splitlines()]
        self.mutations = [call for call in self.calls if call[0] in ('add', 'commit', 'tag', 'push')]
        return result

    def test_bumps_and_explicit_version(self):
        for spec, expected in [('patch', '1.2.4'), ('minor', '1.3.0'), ('major', '2.0.0'), ('2.10.0', '2.10.0')]:
            with self.subTest(spec=spec):
                result = self.run_release(spec, '--dry-run', '--push')
                self.assertEqual(result.returncode, 0, result.stderr)
                self.assertIn(f'Release: v{expected}', result.stdout)
                self.assertIn(f'Commit: Release v{expected}', result.stdout)
                self.assertEqual(self.settings.read_bytes(), self.original)
                self.assertEqual(self.mutations, [])

    def test_invalid_or_non_increasing_versions(self):
        for spec in ['1.2.3', '1.0.0', '01.3.0', '2.0.0-rc.1', 'garbage']:
            with self.subTest(spec=spec):
                self.assertNotEqual(self.run_release(spec, '-n').returncode, 0)
                self.assertEqual(self.mutations, [])
                self.assertEqual(self.settings.read_bytes(), self.original)

    def test_missing_duplicate_or_invalid_bundle_version(self):
        for value in [b'PlayerSettings:\n', self.original + b'  bundleVersion: 1.0.0\n',
                      self.original.replace(b'1.2.3', b'bad')]:
            self.settings.write_bytes(value)
            self.assertNotEqual(self.run_release('--yes').returncode, 0)
            self.assertEqual(self.settings.read_bytes(), value)
            self.assertEqual(self.mutations, [])

    def test_preflight_failures(self):
        for mode in ['dirty', 'shallow', 'detached', 'existing', 'no-remote']:
            with self.subTest(mode=mode):
                self.assertNotEqual(self.run_release('--yes', '--push', mode=mode).returncode, 0)
                self.assertEqual(self.settings.read_bytes(), self.original)
                self.assertEqual(self.mutations, [])

    def test_dirty_dry_run(self):
        result = self.run_release('-n', mode='dirty')
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(self.settings.read_bytes(), self.original)
        self.assertEqual(self.mutations, [])

    def test_confirmation_declined(self):
        self.assertNotEqual(self.run_release().returncode, 0)
        self.assertEqual(self.settings.read_bytes(), self.original)
        self.assertEqual(self.mutations, [])

    def test_version_only_commit_then_tag_then_push(self):
        result = self.run_release('--yes', '--push', '-m', 'Notes with spaces')
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(self.settings.read_bytes(), self.original.replace(b'1.2.3', b'1.2.4'))
        self.assertEqual(self.mutations, [
            ['add', '--', SETTINGS],
            ['commit', '--only', '-m', 'Release v1.2.4', '--', SETTINGS],
            ['tag', '-a', 'v1.2.4', '-m', 'Notes with spaces'],
            ['push', 'origin', 'main'],
            ['push', 'origin', 'refs/tags/v1.2.4:refs/tags/v1.2.4'],
        ])
        self.assertLess(self.calls.index(['show', 'HEAD:' + SETTINGS]),
                        self.calls.index(['tag', '-a', 'v1.2.4', '-m', 'Notes with spaces']))

    def test_crlf_and_no_push(self):
        original = self.original.replace(b'\n', b'\r\n')
        self.settings.write_bytes(original)
        result = self.run_release('-y')
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(self.settings.read_bytes(), original.replace(b'1.2.3', b'1.2.4'))
        self.assertEqual([c[0] for c in self.mutations], ['add', 'commit', 'tag'])
        self.assertIn('git push origin main', result.stdout)

    def test_failures_stop_later_steps(self):
        for mode, expected in [('add-fail', ['add']), ('commit-fail', ['add', 'commit']),
                               ('committed-mismatch', ['add', 'commit']),
                               ('tag-fail', ['add', 'commit', 'tag']),
                               ('branch-fail', ['add', 'commit', 'tag', 'push'])]:
            with self.subTest(mode=mode):
                self.settings.write_bytes(self.original)
                self.assertNotEqual(self.run_release('-y', '--push', mode=mode).returncode, 0)
                self.assertEqual([c[0] for c in self.mutations], expected)

    @unittest.skipIf(os.name == 'nt', 'The GitHub workflow runs on Ubuntu with Bash.')
    def test_workflow_version_guard(self):
        workflow = (SCRIPTS.parent / '.github/workflows/release.yml').read_text()
        guard = textwrap.dedent(workflow.split('        run: |\n', 1)[1].split('\n      - name:', 1)[0])
        (self.settings.parent / 'ProjectVersion.txt').write_text('m_EditorVersion: 6000.6.0f1\n')
        for tag, settings, expected in [
            ('v1.2.3', self.original, 0),
            ('v1.2.4', self.original, 1),
            ('v1.2.3', b'PlayerSettings:\n', 1),
            ('v1.2.3', self.original + b'  bundleVersion: 1.2.3\n', 1),
            ('v1.2.3-rc.1', self.original, 1),
        ]:
            with self.subTest(tag=tag, settings=settings):
                self.settings.write_bytes(settings)
                result = subprocess.run(['bash', '-e', '-c', guard], cwd=self.root,
                                        env=dict(self.env, RELEASE_TAG=tag), capture_output=True, text=True)
                self.assertEqual(result.returncode, expected, result.stderr)
        self.assertIn('versioning: None', workflow)


if __name__ == '__main__':
    unittest.main()
