const path=require('path');
const sharp=require('/Users/eaafonasev/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
Promise.all(['floor-1','floor-2','floor-3','floor-plans'].map(name=>sharp(path.join(__dirname,name+'.svg')).png().toFile(path.join(__dirname,name+'.png')))).then(()=>console.log('Rendered 3 floor plans and combined sheet.'));
