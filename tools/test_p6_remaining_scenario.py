import importlib.util
import tempfile
import plistlib
import unittest
from pathlib import Path

spec = importlib.util.spec_from_file_location('p6_remaining', Path(__file__).with_name('p6_remaining_scenario.py'))
p6 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(p6)

class RemainingRouteTests(unittest.TestCase):
    def test_only_seven_unexecuted_cases_in_original_order(self):
        self.assertEqual(p6.ORDER, [('lunar-laboratory-v1', x) for x in ['B1','B2','A2']] + [('well-laboratory-v1', x) for x in ['A1','B1','B2','A2']])

    def test_one_real_exclusive_guardian_and_full_scenario_budget(self):
        cmd = p6.outer_command(Path('/runner.py'), Path('/manifest.json'), Path('/fresh'), 2000, 'fresh human', 1000)
        self.assertEqual(cmd[:5], [str(p6.REAL), '--exclusive', '--timeout', '160', '--'])
        self.assertIn('--admitted', cmd)
        with self.assertRaises(ValueError): p6.outer_command(Path('/runner.py'), Path('/m'), Path('/o'), 1800, 'human', 1000)

    def test_late_and_low_capacity_reject_before_player(self):
        self.assertTrue(p6.permitted(1000,1840,7,5*p6.GIB,True))
        self.assertFalse(p6.permitted(1001,1840,7,5*p6.GIB,True))
        self.assertFalse(p6.permitted(1000,1840,7,4*p6.GIB,True))
        self.assertFalse(p6.permitted(1000,1200,1,3*p6.GIB-1))

    def test_direct_admitted_cannot_bypass_real_guardian(self):
        with self.assertRaises(RuntimeError):p6.require_outer_admission('python /runner.py',True)
        with self.assertRaises(RuntimeError):p6.require_outer_admission(str(p6.REAL)+' --exclusive',False)
        p6.require_outer_admission(str(p6.REAL)+' --exclusive -- python /runner.py',True)

    def test_player_path_with_spaces_and_exact_reference_flags(self):
        with tempfile.TemporaryDirectory() as folder:
            app=Path(folder)/'QA Player.app';(app/'Contents').mkdir(parents=True)
            with (app/'Contents/Info.plist').open('wb') as f:plistlib.dump({'CFBundleExecutable':'QA Player'},f)
            cmd=p6.player_command(app,'well-laboratory-v1',Path('/fresh output'),Path('/actual A1/report.json'))
            self.assertEqual(cmd[0],str(app/'Contents/MacOS/QA Player'))
            self.assertEqual(cmd[1:3],['-noaudio','-muted'])
            self.assertEqual(cmd[-2:],['-baselineReference','/actual A1/report.json'])
            self.assertIn('1920',cmd);self.assertIn('1080',cmd)

if __name__ == '__main__':unittest.main()
