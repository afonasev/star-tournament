import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('relay', Path(__file__).parents[1] / 'legacy_release_relay.py')
relay = importlib.util.module_from_spec(spec); spec.loader.exec_module(relay)

class ReleaseRelayContracts(unittest.TestCase):
    def test_initial_repo_is_pinned_and_cdn_is_redirect_only(self):
        good = 'https://github.com/afonasev/star-tournament/releases/download/v1/p.nupkg'
        self.assertTrue(relay.allowed_url(good, initial=True))
        for bad in ['http://github.com/afonasev/star-tournament/releases/download/v1/p.nupkg', good.replace('afonasev', 'other'), 'https://github.com:444/afonasev/star-tournament/releases/download/v1/p.nupkg', good+'?url=other', 'https://user:pass@github.com/afonasev/star-tournament/releases/download/v1/p.nupkg']:
            self.assertFalse(relay.allowed_url(bad, initial=True))
        self.assertFalse(relay.allowed_url('https://release-assets.githubusercontent.com/package', initial=True))
        self.assertTrue(relay.allowed_url('https://release-assets.githubusercontent.com/package?signature=fixture'))

    def test_manifest_rejects_arbitrary_proxy_targets_and_path_traversal(self):
        with tempfile.TemporaryDirectory() as directory:
            p = Path(directory)/'manifest.json'
            item = {'url':'https://github.com/afonasev/star-tournament/releases/download/v1/p.nupkg', 'size':1, 'sha256':'a'*64}
            p.write_text(json.dumps({'/desktop/test/win-x64/p.nupkg':item})); self.assertEqual(len(relay.read_manifest(p)),1)
            for route in ['/elsewhere', '/desktop/test/../private', '/desktop/test/p?url=external']:
                p.write_text(json.dumps({route:item}))
                with self.assertRaises(ValueError): relay.read_manifest(p)

if __name__ == '__main__': unittest.main()
