// Exercise the actual landing selector; no DOM, autoplay or game process.
const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
const source=fs.readFileSync(require('node:path').join(__dirname,'../review-page/downloads.js'),'utf8');
const {installers}=vm.runInNewContext(source.slice(0,source.indexOf('(async()=>{'))+';({installers})');
function release(modern, legacy=false, version='2.0.0-test.10'){
  const names=(modern?['latest-v2-win-x64.json','latest-v2-osx-arm64.json']:[]).concat(legacy?['latest-win-x64.json','latest-osx-arm64.json']:[]);
  const assets=names.map(name=>({name}));
  for(const name of [`Star-Tournament-${version}-Windows-x64-Setup.exe`,`Star-Tournament-${version}-macOS-arm64.pkg`]) assets.push({name,state:'uploaded',size:100,digest:'sha256:'+'a'.repeat(64),browser_download_url:`https://github.com/afonasev/star-tournament/releases/download/v${version}/${name}`});
  return {draft:false,prerelease:version.includes('-'),tag_name:'v'+version,assets};
}
test('modern-only test is eligible',()=>assert.equal(installers(release(true)).version,'2.0.0-test.10'));
test('current production with frozen legacy aliases still selects current installers',()=>assert.equal(installers(release(true,true,'2.0.0')).version,'2.0.0'));
test('historical complete legacy release stays eligible',()=>assert.equal(installers(release(false,true)).version,'2.0.0-test.10'));
test('mixed incomplete metadata families are not a pair',()=>{const r=release(true);r.assets[1].name='latest-osx-arm64.json';assert.equal(installers(r),null)});
test('metadata does not excuse missing or tampered installer',()=>{for(const field of ['state','digest','browser_download_url']){const r=release(true);r.assets[2][field]='invalid';assert.equal(installers(r),null)}});
test('drafts and missing metadata remain ineligible',()=>{assert.equal(installers({...release(true),draft:true}),null);assert.equal(installers(release(false)),null)});
