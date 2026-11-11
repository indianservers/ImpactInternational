import assert from 'node:assert/strict';import {checkpoint,cloneElements,element,physicalPixels,snap} from '../client/src/cards/design.ts';
const results=[];function test(name,fn){fn();results.push({test:name,passed:true});console.log('PASS '+name)}
const a={width:85.6,height:53.98,front:{background:'#ffffff',elements:[element('Text')]},back:{background:'#174f46',elements:[]},bleed:3,safe:3,doubleSided:true};
test('physical coordinates convert to exact 300 DPI pixel bounds',()=>{assert.equal(physicalPixels(85.6),1012);assert.equal(physicalPixels(53.98),638)});
test('move snapshots preserve independent front and back state',()=>{const b=structuredClone(a);b.front.elements[0].x=23.4;const h=checkpoint([a],0,b);assert.equal(h[0].front.elements[0].x,8);assert.equal(h[1].front.elements[0].x,23.4);assert.equal(h[1].back.background,'#174f46')});
test('undo then new change discards redo branch',()=>{const b=structuredClone(a);b.width=60;const c=structuredClone(a);c.width=90;const h=checkpoint([a,b],0,c);assert.equal(h.length,2);assert.equal(h[1].width,90)});
test('history snapshots remain isolated from later edits',()=>{const b=structuredClone(a);const h=checkpoint([a],0,b);b.width=100;assert.equal(h[1].width,85.6)});
test('bounded history retains most recent 80 states',()=>{let h=[a];for(let n=0;n<100;n++)h=checkpoint(h,h.length-1,{...a,width:n+20});assert.equal(h.length,80);assert.equal(h[79].width,119)});
test('smart guide snaps card-centered elements without display-pixel drift',()=>{const result=snap(25.1,35,85.6,false);assert.equal(result.value,25.299999999999997);assert.equal(result.guide,42.8)});
test('millimetre grid snap is independent of screen zoom',()=>assert.equal(snap(13.45,10,85.6,true).value,13));
test('duplicate creates new identities with millimetre offsets and unlocks copies',()=>{const e={...element('Image'),locked:true};const [copy]=cloneElements([e],[e.id]);assert.notEqual(copy.id,e.id);assert.equal(copy.x,e.x+2);assert.equal(copy.locked,false);assert.equal(e.locked,true)});
await import('node:fs/promises').then(fs=>fs.writeFile('test-results/idcard-design.json',JSON.stringify(results,null,2)));
