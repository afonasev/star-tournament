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

    def test_resume_only_unexecuted_optimized_case(self):
        self.assertEqual(runner.case_order({}),('control','optimized'))
        self.assertEqual(runner.case_order({'cases':['optimized']}),('optimized',))
        for order in [[],['control'],['optimized','control'],['control','control'],['optimized','optimized']]:
            with self.assertRaises(ValueError):runner.case_order({'cases':order})

    def test_unprofiled_qualification_keeps_abba_and_admission_budget(self):
        m={'mode':'unprofiled-abba','cases':['A1','B1','B2','A2']}
        self.assertEqual(runner.case_order(m),('A1','B1','B2','A2'))
        self.assertEqual([runner.case_variant(x) for x in runner.case_order(m)],['control','optimized','optimized','control'])
        self.assertEqual(runner.case_budget(m),120)
        self.assertTrue(runner.permitted(0,480,4,13*runner.GIB,runner.case_budget(m)))
        self.assertFalse(runner.permitted(0,479,4,13*runner.GIB,runner.case_budget(m)))
        for order in [[],['A1','B1'],['A1','B1','A2','B2']]:
            with self.assertRaises(ValueError):runner.case_order({'mode':'unprofiled-abba','cases':order})
        with self.assertRaises(ValueError):runner.case_order({'mode':'unknown'})

    def test_three_variant_causal_control_is_balanced_and_complete(self):
        m={'mode':'unprofiled-abccba','cases':['A1','B1','C1','C2','B2','A2']}
        self.assertEqual([runner.case_variant(x) for x in runner.case_order(m)],['control','optimized','corrected','corrected','optimized','control'])
        self.assertTrue(runner.permitted(0,720,6,8*runner.GIB,runner.case_budget(m),runner.case_storage(m)))
        self.assertFalse(runner.permitted(0,719,6,8*runner.GIB,runner.case_budget(m),runner.case_storage(m)))
        self.assertFalse(runner.permitted(0,720,6,8*runner.GIB-1,runner.case_budget(m),runner.case_storage(m)))
        self.assertEqual(runner.case_storage({'mode':'cpu'}),2*runner.GIB)
        for order in [['A1','B1','B2','A2'],['A1','C1','B1','C2','B2','A2']]:
            with self.assertRaises(ValueError):runner.case_order({'mode':'unprofiled-abccba','cases':order})

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
            unprofiled=runner.command(app,output,ref,cpu_profile=False)
            self.assertNotIn('-baselineCpuProfile',unprofiled)
            for flag in ['-noaudio','-muted','-renderingBaseline','-baselineReference']:self.assertIn(flag,unprofiled)
            self.assertNotIn('-profiler-enable',unprofiled)
            self.assertNotIn('-profiler-log-file',unprofiled)

if __name__ == '__main__':unittest.main()
