import importlib.util
from pathlib import Path
import plistlib
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('profile_runner', Path(__file__).with_name('well_frame_profile.py'))
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)

class ProfileRunnerTests(unittest.TestCase):
    def test_complete_pair_budget_and_storage(self):
        self.assertTrue(runner.permitted(100, 400, 2, 9*runner.GIB))
        self.assertFalse(runner.permitted(100, 399, 2, 9*runner.GIB))
        self.assertFalse(runner.permitted(100, 400, 2, 9*runner.GIB-1))

    def test_remaining_case_reserves_profile_plus_5gib(self):
        self.assertTrue(runner.permitted(100, 250, 1, 7*runner.GIB))
        self.assertFalse(runner.permitted(100, 249, 1, 7*runner.GIB))
        self.assertFalse(runner.permitted(100, 250, 1, 7*runner.GIB-1))

    def test_direct_admitted_route_refused(self):
        for parent, leader in [('python run --admitted', True), (str(runner.REAL)+' --shared', True),
                               (str(runner.REAL)+' --exclusive', False)]:
            with self.assertRaises(RuntimeError): runner.require_guardian(parent, leader)
        runner.require_guardian(str(runner.REAL)+' --exclusive -- python', True)

    def test_reference_preserves_original_source_binding(self):
        reference={'source':'original-control','map':'well-laboratory-v1','status':'COMPLETE_DIAGNOSTIC_NOT_ACCEPTANCE'}
        runner.reference_identity(reference,'original-control')
        with self.assertRaises(AssertionError):runner.reference_identity(reference,'rebuilt-control')
        for field,value in [('map','other-map'),('status','INCOMPLETE')]:
            changed=dict(reference);changed[field]=value
            with self.assertRaises(AssertionError):runner.reference_identity(changed,'original-control')

    def test_immutable_player_command_silent_and_exact_profile_reference(self):
        with tempfile.TemporaryDirectory() as d:
            app = Path(d)/'old QA.app';(app/'Contents').mkdir(parents=True)
            with (app/'Contents/Info.plist').open('wb') as f:plistlib.dump({'CFBundleExecutable': 'Retained Player'}, f)
            output=Path(d)/'new evidence';ref=Path(d)/'reference.json'
            cmd=runner.command(app,output,ref)
            self.assertEqual(cmd[0],str(app/'Contents/MacOS/Retained Player'))
            for flag in ['-noaudio','-muted','-renderingBaseline','-baselineCpuProfile']:self.assertIn(flag,cmd)
            self.assertEqual(cmd[cmd.index('-baselineReference')+1],str(ref))
            self.assertEqual(cmd[cmd.index('-baselineCpuProfile')+1],str(output))
            self.assertNotIn('-profiler-enable',cmd)
            self.assertNotIn('-profiler-log-file',cmd)
            self.assertNotIn('-nographics',cmd)
            self.assertNotIn('-batchmode',cmd)
            self.assertNotIn('-deepProfiling',cmd)

if __name__ == '__main__':unittest.main()
