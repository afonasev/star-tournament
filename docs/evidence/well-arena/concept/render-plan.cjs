const fs=require('fs');
const path=require('path');
const vm=require('vm');
const sharp=require('/Users/eaafonasev/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const html=fs.readFileSync(path.join(__dirname,'index.html'),'utf8');
const source=html.match(/<script>([\s\S]*?)<\/script>/)[1];
const map={innerHTML:''},description={innerHTML:''};
const buttons=[0,1,2].map(level=>({dataset:{level},classList:{toggle(){}}}));
const ctx=vm.createContext({document:{querySelector:s=>s==='#map'?map:description,querySelectorAll:()=>buttons}});
vm.runInContext(source,ctx);
let panels='';
const titles=['01 / БИОРАЗРАБОТКИ','02 / ФИЗИКА','03 / ЭЛЕКТРОНИКА'];
const colors=['#38865b','#287db2','#a78317'];
for(let i=0;i<3;i++){
  buttons[i].onclick();
  if(!map.innerHTML.includes('<svg')||!description.innerHTML.includes('<h2>'))throw Error('Missing floor '+i);
  const body=map.innerHTML.replace(/^<svg[^>]*>/,'').replace(/<\/svg>$/,'').replaceAll('grate',`grate${i}`);
  panels+=`<g transform="translate(${30+i*610} 130)"><text x="300" y="-22" font-family="sans-serif" text-anchor="middle" font-size="22" font-weight="bold" fill="${colors[i]}">${titles[i]}</text>${body}</g>`;
}
const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="1890" height="875" viewBox="0 0 1890 875"><rect width="1890" height="875" fill="#f5f7f6"/><text x="50" y="47" font-family="sans-serif" font-size="32" font-weight="bold" fill="#22313b">КОЛОДЕЦ / план связей и предметов</text><text x="1840" y="43" text-anchor="end" font-family="sans-serif" font-size="15" fill="#62767b">КОНЦЕПТ 01 · НЕ РАБОЧИЙ ЧЕРТЁЖ</text>${panels}<g font-family="sans-serif" font-size="17" fill="#334750"><text x="50" y="790">Л — лифт   СТ — лестница   S — область возрождения   Д — дробовик   Р — ракетница   Э — резак</text><text x="50" y="820">Б — броня   У — ускорение   D — урон   ♥ — полное лечение</text><text x="1030" y="790">Голубые окна: непробиваемое стекло</text><text x="1030" y="820">Оранжевый пунктир: открытый проём · Цветной пунктир: обход этажа</text></g></svg>`;
fs.writeFileSync(path.join(__dirname,'floor-plans.svg'),svg);
sharp(Buffer.from(svg)).png().toFile(path.join(__dirname,'floor-plans.png')).then(()=>console.log('Three floor selectors executed; static SVG and PNG exported.'));
