const {chromium}=require('playwright');
const path=require('path');
(async()=>{
const browser=await chromium.launch({headless:true});
const page=await browser.newPage({viewport:{width:1600,height:900},deviceScaleFactor:1});
await page.goto('file://'+path.join(__dirname,'index.html'));
await page.evaluate(()=>document.fonts.ready);
for(const id of ['a-start','a-match','b-start','b-match']) {
 await page.locator('#'+id).screenshot({path:path.join(__dirname,id+'.png'),animations:'disabled'});
}
await page.setViewportSize({width:1280,height:720});
await page.reload();
const issues=await page.evaluate(()=>[...document.querySelectorAll('.screen')].flatMap(s=>[...s.querySelectorAll('h1,h2,p,.meta,.status,.brand')].filter(e=>e.scrollWidth>e.clientWidth+1).map(e=>s.id+': '+e.textContent)));
if(issues.length)throw Error(JSON.stringify(issues));
await browser.close();
console.log('Rendered four 1600x900 concepts; checked text overflow at 1280x720.');
})();
