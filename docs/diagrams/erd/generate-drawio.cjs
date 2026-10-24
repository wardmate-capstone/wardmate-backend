// Three unified, editable pages plus SVG previews. No external packages.
const fs=require('fs'),path=require('path'),d=__dirname;
const m=JSON.parse(fs.readFileSync(path.join(d,'model-inventory.json'),'utf8'));
const esc=s=>String(s).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;');
const colors={IAM:'#dbeafe',CAT:'#dcfce7',WF:'#fef3c7',DOC:'#ede9fe',AI:'#ffe4e6',SYS:'#cffafe'};
const names={IAM:'IAM',CAT:'Procedure Catalog',WF:'Application Workflow',DOC:'Document Form',AI:'AI / OCR',SYS:'Analytics / Notifications'};
// knowledge_documents/knowledge_chunks are implemented since 24/10/2026 (TASK-28/29) and come from the model inventory.
const fn=['ai_request_logs','ocr_scan_logs','audit_logs','system_settings','service_health_logs','system_backup_logs'];
const future=fn.map((n,i)=>({id:'FUTURE_'+n,label:n,group:i<2?'AI':'SYS',future:true,lines:['FUTURE - schema not implemented']}));
const fe=[['AI_chat_sessions','FUTURE_ai_request_logs','proposed request log'],['IAM_users','FUTURE_ocr_scan_logs','proposed actor'],['IAM_users','FUTURE_audit_logs','proposed actor'],['IAM_users','FUTURE_system_settings','proposed editor']].map(([from,to,label])=>({from,to,label,ref:true,future:true}));
const edges=[...m.edges.map(e=>({...e,label:e.label+' ['+e.left+' -> '+e.right+']'})),...m.refs.map(([from,to,label])=>({from,to,label:'REF '+label,ref:true})),...fe];
function node(t,physical){return {id:t.id,group:t.group,label:physical?(t.sqlName.includes('.')?t.sqlName:'public.'+t.sqlName):t.name,lines:t.cols.map(c=>[t.pk.includes(c.name)?'PK':'',t.fks.some(f=>f.cols.includes(c.name))?'FK':'',t.unique.some(u=>u.includes(c.name))?'UK*':''].filter(Boolean).join(',').padEnd(9)+c.name+(physical?' : '+c.type+(c.nullable?' NULL':' NOT NULL'):'' )).concat(physical?t.unique.map(u=>'UNIQUE ('+u.join(', ')+')'):[])};}
function page(name,nodes,edges){
 const simple=name==='Conceptual',cols=simple?5:6,gap=240,top=420,left=80;
 const width=simple?300:Math.max(500,...nodes.map(n=>Math.max(n.label.length,...n.lines.map(l=>l.length))*8.5+30));
 const heights=Array(Math.ceil(nodes.length/cols)).fill(0);
 nodes.forEach((n,i)=>{n.col=i%cols;n.row=Math.floor(i/cols);n.h=simple?100:64+n.lines.length*23;heights[n.row]=Math.max(heights[n.row],n.h);});
 const ys=[top];for(let r=1;r<heights.length;r++)ys[r]=ys[r-1]+heights[r-1]+260;
 const pos=new Map(nodes.map(n=>{n.x=left+n.col*(width+gap);n.y=ys[n.row];return[n.id,n];}));
 let svg=[],cells=['<mxCell id="0"/>','<mxCell id="1" parent="0"/>'];
 const text=(x,y,s,size=16,bold=false)=>`<text x="${x}" y="${y}" font-family="Arial,sans-serif" font-size="${size}" font-weight="${bold?'bold':'normal'}" fill="#172b4d">${esc(s)}</text>`;
 function box(id,x,y,w,h,value,style){cells.push(`<mxCell id="${id}" value="${esc(value)}" style="${style}" vertex="1" parent="1"><mxGeometry x="${x}" y="${y}" width="${w}" height="${h}" as="geometry"/></mxCell>`);}
 svg.push(text(left,55,'WARDMATE | '+name.toUpperCase()+' ERD',34,true),text(left,90,(simple?'ONE SYSTEM / SIX SERVICES | Business relationships | Gray: FUTURE':'ONE SYSTEM / SIX SERVICES | Solid: FK | Dashed: logical REF | Gray: FUTURE'),18),text(left,120,'Rxx links to the relationship register below. REF is not a database foreign key.',16));
 box('title',left,20,2400,110,'WARDMATE | '+name+' ERD\nOne system / six services\nSolid = FK; dashed = REF; gray = FUTURE. Rxx: relationship register below.','text;html=0;align=left;verticalAlign=top;fontSize=20;fontStyle=1;');
 Object.keys(names).forEach((g,i)=>{const x=left+i*235;svg.push(`<rect x="${x}" y="155" width="225" height="36" rx="5" fill="${colors[g]}"/>`,text(x+10,179,names[g],14));box('legend_'+g,x,155,225,36,names[g],`rounded=1;html=0;fillColor=${colors[g]};fontSize=14;`);});
 const ports={},rails={},roads={},routes=[];
 edges.forEach((e,i)=>{
  const a=pos.get(e.from),b=pos.get(e.to);if(!a||!b)throw Error('Unknown endpoint');
  const port=n=>{const v=ports[n.id]||0;ports[n.id]=v+1;return Math.min(n.h-12,26+v*4);};
  const rail=n=>{const v=rails[n.col]||0;rails[n.col]=v+1;return n.x+width+20+v*3;};
  const ay=a.y+port(a),by=b.y+port(b),ax=rail(a),bx=rail(b),row=Math.min(a.row,b.row),road=roads[row]||0;roads[row]=road+1;
  const y=ys[row]-30-road*4,pts=[[a.x+width,ay],[ax,ay],[ax,y],[bx,y],[bx,by],[b.x+width,by]],id='R'+String(i+1).padStart(2,'0'),color=e.future?'#94a3b8':e.ref?'#55718f':'#23384f';
  routes.push({id,e,pts});
  svg.push(`<polyline points="${pts.map(p=>p.join(',')).join(' ')}" fill="none" stroke="${color}" stroke-width="1.5" ${e.ref?'stroke-dasharray="7 5"':''} marker-end="url(#arrow)"/>`);
  cells.push(`<mxCell id="${id}" value="${id}" style="html=0;noEdgeStyle=1;endArrow=open;dashed=${e.ref?1:0};strokeColor=${color};fontSize=12;labelBackgroundColor=#ffffff;exitX=1;exitY=${(ay-a.y)/a.h};entryX=1;entryY=${(by-b.y)/b.h};" edge="1" parent="1" source="${e.from}" target="${e.to}"><mxGeometry relative="1" as="geometry"><Array as="points">${pts.slice(1,-1).map(([x,y])=>`<mxPoint x="${x}" y="${y}"/>`).join('')}</Array></mxGeometry></mxCell>`);
 });
 nodes.forEach(n=>{
  const fill=colors[n.group]||'#e2e8f0';
  svg.push(`<rect x="${n.x}" y="${n.y}" width="${width}" height="${n.h}" rx="6" fill="white" stroke="#334155" stroke-width="1.5" ${n.future?'stroke-dasharray="8 5"':''}/>`,`<rect x="${n.x+1}" y="${n.y+1}" width="${width-2}" height="52" rx="5" fill="${fill}"/>`,text(n.x+12,n.y+20,(n.future?'FUTURE / ':'')+(names[n.group]||'Business entity'),12),text(n.x+12,n.y+43,n.label,simple?17:18,true));
  n.lines.forEach((l,j)=>svg.push(`<text x="${n.x+12}" y="${n.y+74+j*23}" font-family="Consolas,monospace" font-size="14" fill="#1e293b">${esc(l)}</text>`));
  const html=`<div style="background:${fill};padding:9px;font-family:Arial;font-size:12px">${esc((n.future?'FUTURE / ':'')+(names[n.group]||'Business entity'))}<br/><b style="font-size:18px">${esc(n.label)}</b></div><div style="padding:8px 10px;font-family:Consolas,monospace;font-size:14px;line-height:23px;white-space:pre">${n.lines.map(esc).join('<br/>')}</div>`;
  box(n.id,n.x,n.y,width,n.h,html,`rounded=1;html=1;align=left;verticalAlign=top;spacing=0;overflow=fill;fillColor=#ffffff;strokeColor=#334155;dashed=${n.future?1:0};`);
 });
 // Place identifiers in road space, not over entity text.
 routes.forEach(({id,pts})=>{let[x,y]=pts[2];svg.push(`<rect x="${x-2}" y="${y-17}" width="36" height="17" fill="white"/>`,text(x,y-4,id,11,true));});
 const bottom=ys.at(-1)+heights.at(-1)+100,totalW=left+cols*(width+gap),regRows=Math.ceil(edges.length/3),regW=totalW/3;
 svg.push(text(left,bottom,'RELATIONSHIP REGISTER',26,true),text(left,bottom+30,'PK = primary key | FK = database constraint | UK* = member of a UNIQUE key; see grouped UNIQUE in Physical.',16));
 routes.forEach(({id,e},i)=>{const x=left+Math.floor(i/regRows)*regW,y=bottom+75+(i%regRows)*70,lines=[id+'  '+e.from+' -> '+e.to,e.label];svg.push(text(x,y,lines[0],13,true),text(x,y+22,lines[1],12));box('register_'+id,x,y-18,regW-24,60,lines.join('\n'),'text;html=0;align=left;verticalAlign=top;fontSize=13;');});
 const totalH=bottom+110+regRows*70;
 fs.writeFileSync(path.join(d,name.toLowerCase()+'.svg'),`<svg xmlns="http://www.w3.org/2000/svg" width="${totalW}" height="${totalH}" viewBox="0 0 ${totalW} ${totalH}"><defs><marker id="arrow" markerWidth="9" markerHeight="9" refX="8" refY="4" orient="auto"><path d="M0,0 L8,4 L0,8" fill="none" stroke="#475569"/></marker></defs><rect width="100%" height="100%" fill="#f8fafc"/>${svg.join('\n')}</svg>`);
 for(const {id,e,pts}of routes)for(let j=1;j<pts.length;j++)for(const n of nodes){if(n.id===e.from||n.id===e.to)continue;const[x1,y1]=pts[j-1],[x2,y2]=pts[j];const hit=x1===x2?x1>n.x&&x1<n.x+width&&Math.max(y1,y2)>n.y&&Math.min(y1,y2)<n.y+n.h:y1>n.y&&y1<n.y+n.h&&Math.max(x1,x2)>n.x&&Math.min(x1,x2)<n.x+width;if(hit)throw Error(id+' crosses '+n.id);}
 console.log(`${name}: ${nodes.length} entities, ${edges.length} relationships; no route crosses unrelated tables`);
 return `<diagram name="${name}" id="${name}"><mxGraphModel grid="1" gridSize="10" page="0"><root>${cells.join('')}</root></mxGraphModel></diagram>`;
}
const cg={USER:'IAM',ROLE:'IAM',PERMISSION:'IAM',WARD:'IAM',AUTH_CHALLENGE:'IAM',WELCOME_MESSAGE:'IAM',CATEGORY:'CAT',PROCEDURE:'CAT',PROCEDURE_VERSION:'CAT',PROCEDURE_DRAFT:'CAT',APPLICATION:'WF',CHECKLIST_ITEM:'WF',REVIEW_VERSION:'WF',REVIEW_COMMENT:'WF',STATUS_HISTORY:'WF',DOCUMENT:'DOC',FORM_TEMPLATE:'DOC',FILLED_FORM:'DOC',QR_REFERENCE:'DOC',CHAT_SESSION:'AI',CHAT_MESSAGE:'AI',CITIZEN_FEEDBACK:'SYS',NOTIFICATION:'SYS',DELIVERY:'SYS',PERFORMANCE_METRIC:'SYS',ASSIGNMENT_HISTORY:'WF',DELIVERY_AUDIT:'SYS',KNOWLEDGE_DOCUMENT:'AI',KNOWLEDGE_CHUNK:'AI'};
const cn=new Set(),ce=[];
for(const line of fs.readFileSync(path.join(d,'conceptual.mmd'),'utf8').split('\n')){const v=line.match(/^\s*(\w+)\s+(\S+)\s+(\w+)\s+:\s+(\w+)/);if(v){cn.add(v[1]);cn.add(v[3]);ce.push({from:v[1],to:v[3],label:v[4]+' ['+v[2]+']',ref:cg[v[1]]!==cg[v[3]]});}}
const cm={AI_chat_sessions:'CHAT_SESSION',AI_chat_messages:'CHAT_MESSAGE',IAM_users:'USER'};
const pages=[page('Conceptual',[...cn].map(id=>({id,label:id.replaceAll('_',' '),group:cg[id],lines:[]})).concat(future.map(n=>({...n,lines:[]}))),[...ce,...fe.map(e=>({...e,from:cm[e.from]||e.from,to:cm[e.to]||e.to}))]),page('Logical',[...m.tables.map(t=>node(t,false)),...future.map(n=>({...n}))],edges),page('Physical',[...m.tables.map(t=>node(t,true)),...future.map(n=>({...n}))],edges)];
fs.writeFileSync(path.join(d,'wardmate-erd.drawio'),'<mxfile host="app.diagrams.net">'+pages.join('')+'</mxfile>');
console.log('Exactly three unified pages and three SVGs generated.');
// Presentation documentation is regenerated together with the three-page artifact.
const columns=m.tables.reduce((n,t)=>n+t.cols.length,0);
const note='Mỗi trang chứa toàn bộ 6 service và các quan hệ xuyên service. Nét liền là FK thật; nét đứt là REF logic, không phải FK xuyên database. Các ô FUTURE là '+fn.length+' bảng dự kiến, chưa có schema triển khai. R01… đối chiếu danh mục quan hệ ngay dưới mỗi hình.\n';
for(const [title,file,image,detail]of [
 ['Conceptual','system-erd-conceptual.md','conceptual','Khái niệm nghiệp vụ; không biểu diễn từng cột SQL. DOCUMENT là khái niệm gom tài liệu, không phải một bảng duy nhất.'],
 ['Logical','system-unified-erd.md','logical',m.tables.length+' bảng hiện có, '+fn.length+' bảng dự kiến. Hiển thị thuộc tính và khóa; không đưa kiểu lưu trữ SQL vào từng thuộc tính.'],
 ['Physical','system-erd-physical.md','physical',m.tables.length+' bảng hiện có, '+columns+' cột, '+m.edges.length+' FK thật và '+m.refs.length+' nhóm REF. Hiển thị tên SQL, kiểu dữ liệu, NULL/NOT NULL, PK/FK và UNIQUE ghép. UK* chỉ thành viên của khóa UNIQUE, không có nghĩa mỗi cột riêng lẻ là unique.']
]){
 fs.writeFileSync(path.join(d,'../../',file),`# WardMate — ${title} ERD\n\nCập nhật 24/10/2026 (UTC+07:00). Bản tương tác và draw.io tiếng Anh: [wardmate-erd-interactive.html](diagrams/erd/wardmate-erd-interactive.html), [wardmate-erd-en.drawio](diagrams/erd/wardmate-erd-en.drawio).\n\n${note}\n${detail}\n\n[ Mở hình SVG để phóng to ](diagrams/erd/${image}.svg) · [File draw.io — chọn trang ${title}](diagrams/erd/wardmate-erd.drawio)\n\n![${title} ERD](diagrams/erd/${image}.svg)\n\nNguồn: [EF model inventory](diagrams/erd/model-inventory.json). [DDL đầy đủ](diagrams/erd/schema/) giữ default, index, CHECK và ON DELETE; đây là script CREATE để tham khảo, không chạy lên DB hiện hữu. Sơ đồ đối chiếu source, không xác nhận migration trên Azure. Không tạo bảng hay sửa code backend trong lượt này.\n`);
}
fs.writeFileSync(path.join(d,'README.md'),`# WardMate — ba trang ERD toàn hệ thống\n\nMở **wardmate-erd.drawio** bằng diagrams.net → File → Open From → Device. Có đúng 3 tab: **Conceptual**, **Logical**, **Physical**. Không có tab theo service.\n\n${note}\n- [Conceptual SVG](conceptual.svg)\n- [Logical SVG](logical.svg)\n- [Physical SVG](physical.svg)\n\nSVG có thể phóng to mà không vỡ chữ. Physical là canvas lớn để chứa toàn bộ cột của mọi bảng; không ép về một trang A4. Màu tiêu đề chỉ ra service sở hữu bảng, không tách thành sơ đồ riêng.\n\nTái sinh bằng node docs/diagrams/erd/generate-erd.cjs (đọc DDL đã xuất), sau đó node docs/diagrams/erd/generate-drawio.cjs. Các file physical-*.mmd và archive-* là nguồn tham khảo/bản cũ, không phải bộ 3 trang bàn giao.\n\nDDL schema/*.sql chỉ để tham khảo, không phải migration nâng cấp. Không tạo FK xuyên service. Chưa xác nhận trạng thái DB Azure.\n`);
let doc=fs.readFileSync(path.join(d,'../../system-diagrams.md'),'utf8');let start=doc.indexOf('## 1. ERD'),end=doc.indexOf('## 2.',start);
if(start>=0&&end>start)doc=doc.slice(0,start)+'## 1. ERD — ba trang toàn hệ thống\n\n[Conceptual](system-erd-conceptual.md) · [Logical](system-unified-erd.md) · [Physical](system-erd-physical.md) · [File draw.io 3 trang](diagrams/erd/wardmate-erd.drawio). Mỗi trang nối toàn bộ 6 service. '+m.tables.length+' bảng hiện có; '+fn.length+' bảng FUTURE được đánh dấu riêng trong cùng hình. Bản tương tác: [wardmate-erd-interactive.html](diagrams/erd/wardmate-erd-interactive.html); draw.io tiếng Anh: [wardmate-erd-en.drawio](diagrams/erd/wardmate-erd-en.drawio).\n\n'+doc.slice(end);
fs.writeFileSync(path.join(d,'../../system-diagrams.md'),doc);

