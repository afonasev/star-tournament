import hashlib
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
sys.path.insert(0,str(Path(__file__).parents[1]))
import publish_release as publisher
import upload_github_assets as uploader

class GithubPublication(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup)
        p=Path(self.temp.name)/'asset.json';p.write_text('exact bytes')
        self.assets=[publisher.identity(p)]
        self.remote=dict(id=1,draft=True,prerelease=True,assets=[dict(name=p.name,size=p.stat().st_size,digest='sha256:'+self.assets[0]['sha256'],state='uploaded')],html_url='https://github.com/afonasev/star-tournament/releases/tag/v1.0.0-test.1')
    def test_publish_only_after_readback(self):
        public=dict(self.remote,draft=False)
        with patch.object(publisher,'api',side_effect=[None,self.remote,self.remote,public]) as api,patch.object(publisher,'upload') as upload,patch.object(publisher,'readback',return_value=[]) as readback:
            publisher.publish('1.0.0-test.1','2026-10-06','test',self.assets,'fixture','main')
            self.assertEqual(api.call_args_list[-1].args[-1],dict(draft=False,prerelease=True,make_latest='false'))
            upload.assert_called_once();readback.assert_called_once()
    def test_incomplete_draft_never_published(self):
        with patch.object(publisher,'api',side_effect=[self.remote,dict(self.remote,assets=[])]) as api,patch.object(publisher,'upload'),patch.object(publisher,'readback') as readback:
            with self.assertRaises(ValueError):publisher.publish('1.0.0-test.1','2026-10-06','test',self.assets,'fixture','main')
            self.assertFalse(any(c.args[0]=='PATCH' for c in api.call_args_list));readback.assert_not_called()
    def test_wrong_digest_never_published(self):
        bad=dict(self.remote,assets=[dict(self.remote['assets'][0],digest='sha256:'+'0'*64)])
        with self.assertRaises(ValueError):publisher.verify_remote(bad,self.assets)
    def test_unknown_extra_asset_rejected(self):
        with self.assertRaises(ValueError):publisher.verify_remote(dict(self.remote,assets=self.remote['assets']+[dict(name='foreign')]),self.assets)
    def test_published_resume_never_mutates_release(self):
        with patch.object(publisher,'api',return_value=dict(self.remote,draft=False)) as api,patch.object(publisher,'upload') as upload,patch.object(publisher,'readback',return_value=[]):
            publisher.publish('1.0.0-test.1','2026-10-06','test',self.assets,'fixture','main')
            self.assertEqual(api.call_count,1);upload.assert_not_called()
    def test_production_explicit_latest(self):
        public=dict(self.remote,draft=False,prerelease=False)
        with patch.object(publisher,'api',side_effect=[None,self.remote,self.remote,public]) as api,patch.object(publisher,'upload'),patch.object(publisher,'readback',return_value=[]):
            publisher.publish('1.0.0','2026-10-06','production',self.assets,'fixture','main')
            self.assertEqual(api.call_args_list[-1].args[-1],dict(draft=False,prerelease=False,make_latest='true'))
    def test_changed_local_input_never_uploaded(self):
        Path(self.assets[0]['path']).write_text('modified')
        with patch.object(uploader,'request',return_value=[]) as remote:
            with self.assertRaises(ValueError):uploader.upload(1,self.assets,'fixture')
            self.assertEqual(remote.call_count,1)
    def test_immutable_collision_never_overwrites(self):
        existing=[dict(name=self.assets[0]['name'],size=self.assets[0]['size'],digest='sha256:'+'0'*64)]
        with patch.object(uploader,'request',return_value=existing) as remote:
            with self.assertRaises(ValueError):uploader.upload(1,self.assets,'fixture')
            self.assertEqual(remote.call_count,1)

if __name__=='__main__':unittest.main()
