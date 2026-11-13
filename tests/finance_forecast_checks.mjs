import fs from 'node:fs';
import {financialForecast} from '../client/src/pages/financeForecast.ts';
const results=[];function check(test,passed){results.push({test,passed:!!passed});if(!passed)throw Error(test);}
try{
 check('No invented empty-history forecast',financialForecast([],'2025-07-31')===null);
 const history=Array.from({length:6},(_,i)=>[{year:2025,month:i+1,kind:'Income',amount:(i+1)*100},{year:2025,month:i+1,kind:'Expense',amount:(i+1)*40}]).flat();
 check('Insufficient completed history withheld',financialForecast(history.slice(0,10),'2025-07-31')===null);
 const rows=financialForecast(history,'2025-07-31');check('Three projected months',rows.length===3);check('Projection dates start after selected period',rows[0].label==='2025-08');check('Projection extends three months',rows[2].label==='2025-10');check('Income uses trailing three active months',rows.every(r=>r.income===500));check('Expense uses separate posted series',rows.every(r=>r.expense===200));
 const withPartial=[...history,{year:2025,month:7,kind:'Income',amount:999999}];check('Incomplete selected month excluded',financialForecast(withPartial,'2025-07-31')[0].income===500);
 check('Input order does not change forecast',financialForecast([...history].reverse(),'2025-07-31')[0].income===500);
 check('Actual input records are not modified',history.length===12&&history[0].amount===100);
}finally{fs.writeFileSync(new URL('../docs/qa/finance-forecast-results.json',import.meta.url),JSON.stringify(results,null,2));console.log(JSON.stringify({passed:results.filter(r=>r.passed).length,failed:results.filter(r=>!r.passed).length}));}
