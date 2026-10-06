// GitHub's API supports browser CORS; release asset URLs are navigation links only.
const RELEASES_API='https://api.github.com/repos/afonasev/star-tournament/releases';
const RELEASES_PAGE='https://github.com/afonasev/star-tournament/releases';
function installers(release){
  if(release.draft || !(release.prerelease ? /^v\d+\.\d+\.\d+-[A-Za-z0-9.-]+$/ : /^v\d+\.\d+\.\d+$/).test(release.tag_name)) return null;
  const version=release.tag_name.slice(1),assets=release.assets||[];
  if(!['latest-win-x64.json','latest-osx-arm64.json'].every(name=>assets.some(a=>a.name===name)))return null;
  const result={};
  for(const [id,name] of [['windows',`Star-Tournament-${version}-Windows-x64-Setup.exe`],['mac',`Star-Tournament-${version}-macOS-arm64.pkg`]]){
    const asset=assets.find(a=>a.name===name);
    if(!asset || asset.state!=='uploaded' || asset.size<=0 || !/^sha256:[a-f0-9]{64}$/i.test(asset.digest||''))return null;
    if(asset.browser_download_url!==`https://github.com/afonasev/star-tournament/releases/download/v${version}/${name}`)return null;
    result[id]=asset;
  }
  return {version,channel:release.prerelease?"test":"production",platforms:result};
}
// SemVer numeric prerelease comparison, including test.9 versus test.10.
function compareVersions(a,b){
  const parts=v=>{const [core,...labels]=v.split("-");return core.split(".").concat(labels.join("-").split("."));},aa=parts(a),bb=parts(b);
  for(let i=0;i<Math.max(aa.length,bb.length);i++){
    if(aa[i]===bb[i])continue;
    if(aa[i]===undefined)return -1;if(bb[i]===undefined)return 1;
    const an=/^\d+$/.test(aa[i]),bn=/^\d+$/.test(bb[i]);
    if(an&&bn){const x=BigInt(aa[i]),y=BigInt(bb[i]);return x<y?-1:x>y?1:0;}
    if(an!==bn)return an?-1:1;
    return aa[i]<bb[i]?-1:1;
  }
  return 0;
}
(async()=>{
  for(const id of ['windows','mac']){
    const button=document.getElementById(id);button.href=RELEASES_PAGE;
    button.classList.remove('disabled');button.removeAttribute('aria-disabled');button.innerHTML='Открыть релизы GitHub <span>↗</span>';
    document.getElementById(id+'-meta').textContent='Выберите установщик своей платформы на GitHub';
  }
  try{
    let best=null;
    const latest=await fetch(`${RELEASES_API}/latest`,{headers:{Accept:'application/vnd.github+json'},signal:AbortSignal.timeout(10000)});
    if(latest.ok){best=installers(await latest.json());if(!best||best.channel!=='production')throw Error('Incomplete production Latest');}
    else if(latest.status!==404)throw Error('Production discovery unavailable');
    for(let page=1;!best || best.channel==='test';page++){
      const response=await fetch(`${RELEASES_API}?per_page=100&page=${page}`,{headers:{Accept:'application/vnd.github+json'},signal:AbortSignal.timeout(10000)});
      if(!response.ok)throw Error('Release discovery unavailable');
      const releases=await response.json();if(!Array.isArray(releases))throw Error('Invalid release list');
      for(const release of releases){const candidate=installers(release);if(candidate&&candidate.channel==='test'&&(!best||compareVersions(candidate.version,best.version)>0))best=candidate;}
      if(releases.length<100)break;
      if(page===10)throw Error('Release discovery exceeds limit');
    }
    if(!best)throw Error('No complete test release');
    document.getElementById('release').textContent=`${best.channel==='test'?'Тестовая версия':'Версия'} ${best.version} · обновления доступны из игры`;
    document.getElementById('channel-label').textContent=best.channel==='test'?'ТЕСТОВЫЙ ВЫПУСК':'ВЫПУСК';
    for(const [id,item] of Object.entries(best.platforms)){
      const button=document.getElementById(id);button.href=item.browser_download_url;
      button.innerHTML='Скачать установщик <span>↓</span>';
      document.getElementById(id+'-meta').textContent=`${Math.round(item.size/1048576)} МБ · ${best.version} · SHA256 ${item.digest.slice(7,23)}…`;
    }
  }catch{
    document.getElementById('release').textContent='Актуальные установщики доступны на GitHub Releases';
  }
})();
