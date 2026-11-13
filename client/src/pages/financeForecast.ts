// Deliberately conservative, explainable forecast. Missing history never becomes an invented trend.
export function financialForecast(activity: {year:number;month:number;kind:string;amount:number}[], through:string) {
  const completeBefore=[through.slice(0,7),new Date().toISOString().slice(0,7)].sort()[0];const months=new Map<string,{income:number;expense:number}>();
  for(const row of activity){const key=`${row.year}-${String(row.month).padStart(2,"0")}`;if(key>=completeBefore)continue;const value=months.get(key)||{income:0,expense:0};if(row.kind==="Income")value.income+=row.amount;else if(row.kind==="Expense")value.expense+=row.amount;months.set(key,value);}
  const history=[...months.entries()].sort(([a],[b])=>a.localeCompare(b));if(history.length<6)return null;
  const last=history.slice(-3);const income=last.reduce((n,[,r])=>n+r.income,0)/3,expense=last.reduce((n,[,r])=>n+r.expense,0)/3;
  const start=new Date(through.slice(0,7)+"-01T00:00:00Z");return [1,2,3].map(offset=>{const date=new Date(start);date.setUTCMonth(date.getUTCMonth()+offset);return{label:date.toISOString().slice(0,7),income,expense};});
}
