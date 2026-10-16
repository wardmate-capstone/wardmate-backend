// Editable UML activity drawings. Run: node docs/diagrams/activities/generate-activities.cjs
// No network, packages, Git, database or application runtime required.
const fs = require('node:fs');
const path = require('node:path');
const out = __dirname;
const node = (id, type, col, row, text = '', status = 'CÓ') => ({ id, type, col, row, text, status });
const a = (id,c,r,t,s) => node(id,'action',c,r,t,s);
const d = (id,c,r,t) => node(id,'decision',c,r,t,'');
const m = (id,c,r) => node(id,'merge',c,r);
const start = () => node('start','initial',1,0);
const end = r => node('end','final',1,r);
const edge = (from,to,label='',rail=null) => ({from,to,label,rail});
const diagrams = [
 {id:'01-iam',title:'IAM · Xác thực và quản lý tài khoản',
  note:'Luồng rút gọn: refresh/logout là thao tác riêng. JWT cũ không tự mất hiệu lực ngay khi quyền đổi.',
  nodes:[start(),a('login',1,1,'Gửi yêu cầu đăng nhập'),d('valid',1,2,'CSRF, mật khẩu và tài khoản hợp lệ?'),
   a('err',0,3,'Trả lỗi đăng nhập / CSRF'),d('retry',0,4,'Thử lại?'),a('token',1,3,'Phát JWT và refresh cookie; lưu digest refresh'),
   a('request',1,5,'Gửi yêu cầu nghiệp vụ với access token'),d('permission',1,6,'Đủ quyền và đúng phạm vi?'),
   a('deny',2,7,'Từ chối thao tác, trả lỗi quyền'),a('manage',1,7,'Xử lý tài khoản / hồ sơ / RBAC theo quyền'),
   a('audit',1,8,'Ghi audit RBAC khi có thay đổi tương ứng'),a('future',1,9,'Đồng bộ audit tập trung và cache quyền','DỰ KIẾN'),m('merge',1,10),end(11)],
  edges:[edge('start','login'),edge('login','valid'),edge('valid','err','Không'),edge('err','retry'),edge('retry','login','Có',40),edge('retry','merge','Không',50),
   edge('valid','token','Có'),edge('token','request'),edge('request','permission'),edge('permission','deny','Không'),edge('permission','manage','Có'),
   edge('manage','audit'),edge('audit','future'),edge('future','merge'),edge('deny','merge','',1100),edge('merge','end')]},
 {id:'02-procedure-catalog',title:'Procedure Catalog · PDF đến thủ tục công bố',
  note:'Bản nháp được lưu mới có draftId. Preview không tự tạo draft. AI không tự xuất bản thủ tục.',
  nodes:[start(),a('upload',1,1,'Tải PDF lên để tạo bản nháp'),d('valid',1,2,'PDF hợp lệ và Blob sẵn sàng?'),
   a('error',0,3,'Báo lỗi file hoặc cấu hình'),d('retry',0,4,'Sửa và thử lại?'),a('save',1,3,'Lưu PDF và draft vào hệ thống'),
   d('enabled',1,4,'Bật extraction?'),a('extract',1,5,'Worker gọi AIOCR; nhận payload và cảnh báo'),a('manual',2,5,'Nhập dữ liệu thủ công từ PDF'),
   m('reviewMerge',1,6),a('review',1,7,'Đối soát; sửa lĩnh vực, cases, checklist và forms'),d('confirmed',1,8,'Đủ dữ liệu, đúng revision và đã xác nhận?'),
   a('publish',1,9,'Publish trong transaction; lưu Procedure và version'),a('index',1,10,'Đồng bộ nguồn thủ tục sang Knowledge Base','DỰ KIẾN'),m('finish',1,11),end(12)],
  edges:[edge('start','upload'),edge('upload','valid'),edge('valid','error','Không'),edge('error','retry'),edge('retry','upload','Có',40),edge('retry','finish','Không',50),
   edge('valid','save','Có'),edge('save','enabled'),edge('enabled','extract','Có'),edge('enabled','manual','Không'),edge('extract','reviewMerge'),edge('manual','reviewMerge'),
   edge('reviewMerge','review'),edge('review','confirmed'),edge('confirmed','review','Chưa',1080),edge('confirmed','publish','Có'),edge('publish','index'),edge('index','finish'),edge('finish','end')]},
 {id:'03-application-workflow',title:'Prepare Documents',
  nodes:[start(),a('create',1,1,'Create Application'),a('fill',1,2,'Complete Forms and Attach Documents'),
   a('submit',1,3,'Submit for Document Review'),m('reviewMerge',1,4),a('review',1,5,'Review Documents'),
   d('complete',1,6,'Documents Complete?'),a('revision',0,7,'Request Revision'),a('fix',0,8,'Revise and Resubmit'),
   a('ready',1,7,'Confirm Document Readiness','DỰ KIẾN'),a('download',1,8,'Download Prepared Documents','DỰ KIẾN'),
   a('visit',1,9,'Visit Ward Office','EXTERNAL'),end(10)],
  edges:[edge('start','create'),edge('create','fill'),edge('fill','submit'),edge('submit','reviewMerge'),edge('reviewMerge','review'),
   edge('review','complete'),edge('complete','revision','No'),edge('revision','fix'),edge('fix','reviewMerge','',40),
   edge('complete','ready','Yes'),edge('ready','download'),edge('download','visit'),edge('visit','end')]},
 {id:'04-document-form',title:'Document Form · Biểu mẫu và PDF kết quả',
  note:'Luồng QR là thiết kế đích. Hiện đã có kết xuất PDF theo request bằng Gotenberg khi đủ điều kiện.',
  nodes:[start(),a('template',1,1,'Tạo phôi, schema/mapping và version biểu mẫu'),a('fill',1,2,'Điền form hoặc tải DOCX đã điền'),
   a('link',1,3,'Lưu Blob; liên kết Application và Checklist'),d('approved',1,4,'Document Readiness Confirmed?'),
   a('block',0,5,'Request Document Review','DỰ KIẾN'),a('clean',1,5,'Prepare Clean Document','DỰ KIẾN'),
   d('qrMode',1,6,'Kết xuất theo luồng nào?'),a('qr',2,7,'Lấy URL tra cứu và chèn QR vào tài liệu','DỰ KIẾN'),m('renderMerge',1,8),
   a('render',1,9,'Chuyển DOCX thành PDF bằng Gotenberg'),d('success',1,10,'Có PDF hợp lệ?'),
   a('error',0,11,'Trả lỗi chuyển đổi; không tạo PDF giả'),a('store',1,11,'Lưu GeneratedDocument và kiểm soát quyền tải'),
   a('event',1,12,'Phát DocumentGenerated cho luồng QR','DỰ KIẾN'),m('merge',1,13),end(14)],
  edges:[edge('start','template'),edge('template','fill'),edge('fill','link'),edge('link','approved'),edge('approved','block','Chưa'),edge('block','merge','',40),
   edge('approved','clean','Rồi'),edge('clean','qrMode'),edge('qrMode','qr','Có QR — dự kiến'),edge('qrMode','renderMerge','PDF hiện tại'),edge('qr','renderMerge'),
   edge('renderMerge','render'),edge('render','success'),edge('success','error','Không'),edge('success','store','Có'),edge('error','merge'),edge('store','event'),edge('event','merge'),edge('merge','end')]},
 {id:'05-ai-rag',title:'AIOCR · Tri thức và hỏi đáp RAG',scope:'TOÀN BỘ LUỒNG DỰ KIẾN',
  note:'Chatbot tư vấn có nguồn; không tự cấp quyền, sửa hồ sơ hoặc phê duyệt. Chưa có database RAG trong source.',
  nodes:[start(),a('source',1,1,'Nạp tài liệu nguồn được phép sử dụng','DỰ KIẾN'),a('chunk',1,2,'Tách chunks, giữ version và metadata nguồn','DỰ KIẾN'),
   a('embed',1,3,'Sinh embedding và lưu chỉ mục vector','DỰ KIẾN'),a('question',1,4,'Nhận câu hỏi; kiểm tra phiên chat và quyền','DỰ KIẾN'),
   a('search',1,5,'Tìm nguồn active phù hợp và được phép truy cập','DỰ KIẾN'),d('enough',1,6,'Có đủ nguồn phù hợp?'),
   a('limited',0,7,'Báo chưa đủ thông tin; hướng dẫn tra cứu','DỰ KIẾN'),a('generate',1,7,'Gọi model với ngữ cảnh và nguồn tham khảo','DỰ KIẾN'),
   d('valid',1,8,'Kết quả và citation hợp lệ?'),a('error',2,9,'Báo lỗi hoặc giới hạn; không khẳng định suy đoán','DỰ KIẾN'),
   a('answer',1,9,'Chuẩn bị câu trả lời có dẫn nguồn','DỰ KIẾN'),m('merge',1,10),a('save',1,11,'Lưu tin nhắn và metadata; trả kết quả','DỰ KIẾN'),end(12)],
  edges:[edge('start','source'),edge('source','chunk'),edge('chunk','embed'),edge('embed','question'),edge('question','search'),edge('search','enough'),
   edge('enough','limited','Không'),edge('enough','generate','Có'),edge('generate','valid'),edge('valid','error','Không'),edge('valid','answer','Có'),
   edge('limited','merge','',45),edge('answer','merge'),edge('error','merge'),edge('merge','save'),edge('save','end')]},
 {id:'06-ai-cccd',title:'AIOCR · Quét CCCD và xác nhận dữ liệu',scope:'TOÀN BỘ LUỒNG DỰ KIẾN',
  note:'OCR CCCD khác OCR PDF. Dữ liệu quét phải được người dùng đối soát; không tự cập nhật IAM.',
  nodes:[start(),a('upload',1,1,'Chọn ảnh CCCD và gửi yêu cầu quét','DỰ KIẾN'),d('valid',1,2,'Quyền và file hợp lệ?'),
   a('invalid',0,3,'Báo lỗi; yêu cầu file phù hợp','DỰ KIẾN'),a('ocr',1,3,'Gọi OCR theo cấu hình nhà cung cấp','DỰ KIẾN'),
   d('read',1,4,'Đọc được dữ liệu?'),a('retryMsg',2,5,'Đề nghị chụp lại hoặc nhập thủ công','DỰ KIẾN'),
   a('normalize',1,5,'Chuẩn hóa dữ liệu, confidence và cảnh báo','DỰ KIẾN'),a('review',1,6,'Người dùng sửa và đối soát kết quả','DỰ KIẾN'),
   d('confirm',1,7,'Đã xác nhận?'),a('apply',1,8,'Gửi dữ liệu tới API form/profile trong quyền','DỰ KIẾN'),d('accepted',1,9,'Service đích validate đạt?'),
   a('saved',1,10,'Lưu dữ liệu được chấp nhận','DỰ KIẾN'),m('merge',1,11),a('log',1,12,'Ghi metadata kết quả, không ghi nguyên CCCD vào log','DỰ KIẾN'),end(13)],
  edges:[edge('start','upload'),edge('upload','valid'),edge('valid','invalid','Không'),edge('invalid','merge','',40),edge('valid','ocr','Có'),edge('ocr','read'),
   edge('read','retryMsg','Không'),edge('retryMsg','merge','',1100),edge('read','normalize','Có'),edge('normalize','review'),edge('review','confirm'),
   edge('confirm','review','Chưa',1050),edge('confirm','apply','Rồi'),edge('apply','accepted'),edge('accepted','review','Không',55),edge('accepted','saved','Có'),edge('saved','merge'),edge('merge','log'),edge('log','end')]},
 {id:'07-notifications',title:'Analytics System · Thông báo đa kênh',
  note:'Sent không có nghĩa Delivered hoặc Read. Requeue thủ công và receipt provider chưa triển khai.',
  nodes:[start(),a('inbox',1,1,'Nhận event; lưu Inbox chống trùng rồi ACK'),a('recipient',1,2,'Tra người nhận và phường từ IAM'),d('eligible',1,3,'Người nhận còn hợp lệ?'),
   a('suppressed',0,4,'Đánh dấu Suppressed; không gửi'),a('delivery',1,4,'Lưu Notification và Delivery'),d('channel',1,5,'Kênh gửi?'),
   a('inapp',0,6,'IN_APP: gửi SignalR và số chưa đọc'),a('email',1,6,'EMAIL: SMTP adapter; cần cấu hình và test thật','MỘT PHẦN'),
   a('sms',2,6,'SMS: gọi adapter nhà cung cấp','DỰ KIẾN'),m('sendMerge',1,7),d('sent',1,8,'Bước gửi thành công?'),
   a('fail',0,9,'Blocked hoặc retry / Dead theo loại lỗi'),a('success',1,9,'Ghi Sent; không coi là đã đọc'),a('receipt',1,10,'Nhận receipt nếu nhà cung cấp hỗ trợ','DỰ KIẾN'),m('merge',1,11),end(12)],
  edges:[edge('start','inbox'),edge('inbox','recipient'),edge('recipient','eligible'),edge('eligible','suppressed','Không'),edge('suppressed','merge','',40),
   edge('eligible','delivery','Có'),edge('delivery','channel'),edge('channel','inapp','IN_APP'),edge('channel','email','EMAIL'),edge('channel','sms','SMS'),
   edge('inapp','sendMerge'),edge('email','sendMerge'),edge('sms','sendMerge'),edge('sendMerge','sent'),edge('sent','fail','Không'),edge('sent','success','Có'),
   edge('success','receipt'),edge('receipt','merge'),edge('fail','merge'),edge('merge','end')]},
 {id:'08-analytics-results',title:'Analytics System · QR, đánh giá và dashboard',scope:'TOÀN BỘ LUỒNG DỰ KIẾN',
  note:'Ba nhánh là các yêu cầu/job riêng. Quyền tra cứu QR, điều kiện đánh giá và grain theo phường cần chốt.',
  nodes:[start(),d('kind',1,1,'Loại yêu cầu / sự kiện?'),
   a('approve',0,2,'Receive Readiness Event','DỰ KIẾN'),a('feedback',1,2,'Nhận đánh giá của công dân','DỰ KIẾN'),a('metric',2,2,'Nhận sự kiện xử lý hồ sơ','DỰ KIẾN'),
   a('qr',0,3,'Tạo mã tra cứu ổn định và phối hợp tạo PDF','DỰ KIẾN'),d('valid',1,3,'Đúng chủ hồ sơ, rating và điều kiện?'),a('aggregate',2,3,'Tổng hợp theo ngày, cán bộ, thủ tục, phường','DỰ KIẾN'),
   a('update',0,4,'Nhận DocumentGenerated; cập nhật link kết quả','DỰ KIẾN'),a('save',1,4,'Lưu citizen_feedback; chống trùng hồ sơ','DỰ KIẾN'),a('dashboard',2,4,'Trả dashboard đúng phạm vi quản lý','DỰ KIẾN'),
   a('reject',1,5,'Trả lỗi đánh giá không hợp lệ','DỰ KIẾN'),m('merge',1,6),end(7)],
  edges:[edge('start','kind'),edge('kind','approve','QR / kết quả'),edge('kind','feedback','Đánh giá'),edge('kind','metric','Thống kê'),edge('approve','qr'),edge('qr','update'),
   edge('feedback','valid'),edge('valid','save','Có'),edge('valid','reject','Không',750),edge('metric','aggregate'),edge('aggregate','dashboard'),edge('update','merge','',45),edge('save','merge','',390),edge('dashboard','merge','',1100),edge('reject','merge'),edge('merge','end')]},
 {id:'09-system-operations',title:'Analytics System · Audit, cấu hình và vận hành',scope:'LUỒNG TẬP TRUNG DỰ KIẾN',
  note:'Hiện có audit RBAC và health endpoint ở một số service. Luồng tập trung/backup dưới đây chưa triển khai.',
  nodes:[start(),d('kind',1,1,'Loại công việc?'),a('audit',0,2,'Nhận audit hoặc yêu cầu đổi cấu hình','DỰ KIẾN'),a('health',1,2,'Scheduler kiểm tra health các service','DỰ KIẾN'),a('backup',2,2,'Scheduler sao lưu theo chính sách','DỰ KIẾN'),
   a('sanitize',0,3,'Kiểm tra quyền và dữ liệu; lọc secret khỏi audit','DỰ KIẾN'),a('history',1,3,'Ghi health history và cảnh báo theo ngưỡng','DỰ KIẾN'),d('verify',2,3,'Backup toàn vẹn?'),
   a('store',0,4,'Lưu audit/settings và phân phối có kiểm soát','DỰ KIẾN'),a('failure',1,4,'Ghi lỗi backup và cảnh báo','DỰ KIẾN'),a('retention',2,4,'Lưu metadata và áp dụng retention','DỰ KIẾN'),
   a('restore',2,5,'Diễn tập restore ở môi trường tách biệt','DỰ KIẾN'),m('merge',1,6),end(7)],
  edges:[edge('start','kind'),edge('kind','audit','Audit / settings'),edge('kind','health','Health'),edge('kind','backup','Backup'),edge('audit','sanitize'),edge('sanitize','store'),
   edge('health','history'),edge('history','merge','',390),edge('backup','verify'),edge('verify','failure','Không'),edge('verify','retention','Có'),edge('retention','restore'),edge('restore','merge'),edge('failure','merge'),edge('store','merge','',40),edge('merge','end')]}
];

// English presentation labels. Keep implementation status in the underlying data,
// while action nodes show only concise verb phrases as requested by the owner.
const english = [
 {title:'IAM - Authenticate User', labels:{login:'Enter Credentials',valid:'Valid Credentials?',err:'Display Login Error',retry:'Retry?',token:'Issue Tokens',request:'Request Operation',permission:'Access Allowed?',deny:'Display Access Error',manage:'Process Operation',audit:'Record RBAC Changes',future:'Synchronize Audit and Permissions',merge:''}},
 {title:'Procedure Catalog - Publish Procedure',labels:{upload:'Upload PDF',valid:'Valid File and Storage?',error:'Display Upload Error',retry:'Retry?',save:'Save Draft',enabled:'Extraction Enabled?',extract:'Extract Procedure Data',manual:'Enter Procedure Data',reviewMerge:'',review:'Review and Edit Draft',confirmed:'Ready to Publish?',publish:'Publish Procedure',index:'Update Knowledge Base',finish:''}},
 {title:'Application Workflow - Prepare for Ward Visit',labels:{}},
 {title:'Document Form - Prepare Documents',labels:{template:'Create Form Template',fill:'Complete Form',link:'Save and Link Documents',approved:'Document Readiness Confirmed?',block:'Request Document Review',clean:'Prepare Clean Document',qrMode:'Output Type?',qr:'Insert Preparation Reference QR',renderMerge:'',render:'Convert Document to PDF',success:'Valid PDF?',error:'Display Conversion Error',store:'Store Prepared PDF',event:'Publish Preparation Event',merge:''}},
 {title:'AIOCR - Answer Questions',labels:{source:'Import Knowledge Sources',chunk:'Split Documents into Chunks',embed:'Create Vector Index',question:'Receive Question',search:'Retrieve Relevant Sources',enough:'Sufficient Evidence?',limited:'Display Information Limit',generate:'Generate Answer',valid:'Valid Answer and Citations?',error:'Display Answer Error',answer:'Prepare Cited Answer',merge:'',save:'Save and Return Response'}},
 {title:'AIOCR - Extract Identity Data',labels:{upload:'Upload Identity Image',valid:'Valid Input?',invalid:'Display Input Error',ocr:'Scan Identity Document',read:'Data Extracted?',retryMsg:'Request New Image or Manual Input',normalize:'Normalize Extracted Data',review:'Review and Edit Data',confirm:'Data Confirmed?',apply:'Submit Confirmed Data',accepted:'Data Accepted?',saved:'Save Accepted Data',merge:'',log:'Record Scan Metadata'}},
 {title:'Analytics System - Deliver Notifications',labels:{inbox:'Receive and Store Event',recipient:'Find Recipients',eligible:'Recipient Eligible?',suppressed:'Suppress Delivery',delivery:'Create Notification',channel:'Delivery Channel?',inapp:'Send In-App Notification',email:'Send Email',sms:'Send SMS',sendMerge:'',sent:'Send Successful?',fail:'Block or Retry Delivery',success:'Record Send Status',receipt:'Process Provider Receipt',merge:''}},
 {title:'Analytics System - Track Preparation and Feedback',labels:{kind:'Request Type?',approve:'Receive Readiness Event',feedback:'Receive Citizen Feedback',metric:'Receive Processing Event',qr:'Create Preparation Reference',valid:'Valid Feedback?',aggregate:'Aggregate Preparation Metrics',update:'Link Prepared Documents',save:'Save Feedback',dashboard:'Display Ward Dashboard',reject:'Display Feedback Error',merge:''}},
 {title:'Analytics System - Run System Operations',labels:{kind:'Operation Type?',audit:'Receive Audit or Settings Request',health:'Check Service Health',backup:'Create Database Backup',sanitize:'Validate Access and Sanitize Data',history:'Record Health and Raise Alerts',verify:'Backup Valid?',store:'Save Audit or Settings',failure:'Record Backup Error',retention:'Store Backup Metadata',restore:'Verify Restore in Isolation',merge:''}}
];
const guards = {'Không':'No','Có':'Yes','Chưa':'No','Rồi':'Yes','Bổ sung':'Revise','SUBMITTED':'Resubmit','Có QR — dự kiến':'With QR','PDF hiện tại':'PDF Only','QR / kết quả':'Preparation','Đánh giá':'Feedback','Thống kê':'Metrics','Audit / settings':'Audit / Settings'};
diagrams.forEach((diagram,i)=>{
 diagram.title=english[i].title; diagram.note=''; diagram.scope='';
 diagram.targetScope=[2,3,7].includes(i);
 diagram.nodes.forEach(n=>{if(n.id in english[i].labels)n.text=english[i].labels[n.id];});
 diagram.edges.forEach(e=>{e.label=guards[e.label]||e.label;});
});
const esc = s => String(s).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&apos;'}[c]));
function wrap(text, max=34) {
 const lines=[]; let line='';
 for(const word of text.split(/\s+/)){ if((line+' '+word).trim().length>max && line){lines.push(line);line=word;}else line=(line+' '+word).trim(); }
 if(line) lines.push(line); return lines;
}
function geometry(n){
 const x=[190,570,950][n.col], y=210+n.row*148;
 const lines=wrap(n.text,n.type==='decision'?24:34);
 const w=n.type==='action'?286:n.type==='decision'?238:n.type==='merge'?24:24;
 const h=n.type==='action'? Math.max(68,24+lines.length*19):n.type==='decision'?108:24;
 return {...n,x,y,w,h,lines};
}
function route(s,t,rail){
 if(rail!==null) {
  if(rail<100) rail=rail<=45?20:34;
  if(s.type==='decision') return [[s.x+Math.sign(rail-s.x)*s.w/2,s.y],[rail,s.y],[rail,t.y-t.h/2-23],[t.x,t.y-t.h/2-23],[t.x,t.y-t.h/2]];
  return [[s.x,s.y+s.h/2],[s.x,s.y+s.h/2+23],[rail,s.y+s.h/2+23],[rail,t.y-t.h/2-23],[t.x,t.y-t.h/2-23],[t.x,t.y-t.h/2]];
 }
 if(t.type==='merge' && s.x!==t.x) return [[s.x,s.y+s.h/2],[s.x,t.y],[t.x+Math.sign(s.x-t.x)*t.w/2,t.y]];
 if(s.type==='decision' && s.x!==t.x) return [[s.x+Math.sign(t.x-s.x)*s.w/2,s.y],[t.x,s.y],[t.x,t.y-t.h/2]];
 if(s.x===t.x) return [[s.x,s.y+s.h/2],[t.x,t.y-t.h/2]];
 const y=(s.y+s.h/2+t.y-t.h/2)/2;
 return [[s.x,s.y+s.h/2],[s.x,y],[t.x,y],[t.x,t.y-t.h/2]];
}
function labelAt(points){
 // Prefer a horizontal segment so branch labels do not cover actions.
 for(let i=0;i<points.length-1;i++){const [a,b]=[points[i],points[i+1]];if(a[1]===b[1]&&Math.abs(a[0]-b[0])>60)return [(a[0]+b[0])/2,a[1]-10];}
 const a=points[0], b=points[1]; return [(a[0]+b[0])/2+28,(a[1]+b[1])/2];
}
function svgFor(diagram){
 const nodes=diagram.nodes.map(geometry), map=new Map(nodes.map(n=>[n.id,n]));
 if(map.size!==nodes.length) throw Error('Duplicate node: '+diagram.id);
 const H=Math.max(...nodes.map(n=>n.y))+120;
 const body=[];
 for(const e of diagram.edges){
  const s=map.get(e.from),t=map.get(e.to); if(!s||!t) throw Error('Unknown node: '+diagram.id);
  const p=route(s,t,e.rail);body.push(`<path d="M${p.map(x=>x.join(',')).join(' L')}" fill="none" stroke="#45525e" stroke-width="1.6" marker-end="url(#arrow)"/>`);
  if(e.label){const [x,y]=labelAt(p),w=e.label.length*7.2+14;body.push(`<rect x="${x-w/2}" y="${y-13}" width="${w}" height="20" rx="4" fill="white"/><text x="${x}" y="${y+2}" class="edgeLabel">${esc(e.label)}</text>`);}
 }
 for(const n of nodes){
  if(n.type==='initial') body.push(`<circle cx="${n.x}" cy="${n.y}" r="11" fill="#111"/>`);
  else if(n.type==='final')body.push(`<circle cx="${n.x}" cy="${n.y}" r="13" fill="white" stroke="#111" stroke-width="2"/><circle cx="${n.x}" cy="${n.y}" r="8" fill="#111"/>`);
  else if(n.type==='merge'||n.type==='decision'){
   body.push(`<path d="M${n.x},${n.y-n.h/2} L${n.x+n.w/2},${n.y} L${n.x},${n.y+n.h/2} L${n.x-n.w/2},${n.y} Z" fill="#7ed0eb" stroke="#45525e" stroke-width="1.6"/>`);
   if(n.type==='decision') body.push(`<text class="nodeText" x="${n.x}" y="${n.y-(n.lines.length-1)*9+5}">${n.lines.map((s,i)=>`<tspan x="${n.x}" dy="${i?18:0}">${esc(s)}</tspan>`).join('')}</text>`);
  }else{
   body.push(`<rect x="${n.x-n.w/2}" y="${n.y-n.h/2}" width="${n.w}" height="${n.h}" rx="18" fill="${n.status==='EXTERNAL'?'#eeeeee':'#7ed0eb'}" stroke="${n.status==='MỘT PHẦN'?'#b7791f':'#45525e'}" stroke-width="1.6" ${n.status==='DỰ KIẾN'?'stroke-dasharray="6 4"':''}/>`);
   body.push(`<text x="${n.x}" y="${n.y-(n.lines.length-1)*9.5+5}" class="nodeText">${n.lines.map((s,i)=>`<tspan x="${n.x}" dy="${i?19:0}">${esc(s)}</tspan>`).join('')}</text>`);
  }
 }
 return `<svg xmlns="http://www.w3.org/2000/svg" width="1140" height="${H}" viewBox="0 0 1140 ${H}" role="img" aria-labelledby="title"><title id="title">${esc(diagram.title)}</title><defs><marker id="arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M1 1 L9 5 L1 9" fill="none" stroke="#45525e" stroke-width="1.4"/></marker></defs><style>text{font-family:Arial,'Segoe UI',sans-serif;fill:#162b3b;text-anchor:middle}.nodeText{font-size:14px}.edgeLabel{font-size:12px}</style><rect width="1140" height="${H}" fill="white"/><text x="570" y="42" font-size="24" font-weight="700">${esc(diagram.title)}</text><text x="570" y="86" font-size="12">Solid border: implemented or partially implemented · Dashed border: planned</text>${diagram.targetScope?'<text x="570" y="112" font-size="12">Target scope: document preparation only. Backend changes pending. Ward visit is outside WardMate.</text>':''}${body.join('\n')}<text x="570" y="${H-27}" font-size="12">WardMate · UML Activity Diagram · 15 Oct 2026</text></svg>`;
}
function mermaidFor(diagram){
 const id = value => value==='end'?'ActivityFinal':value;
 const lines=['%% '+diagram.title, ...(diagram.targetScope?['%% Target scope only. Backend changes pending. Ward visit is outside WardMate.']:[]),'%% Dashed actions are planned; see README.md for partial implementations.','flowchart TD'];
 for(const n of diagram.nodes){
  const label=n.text.replace(/"/g,"'");
  if(n.type==='initial') lines.push(`  ${n.id}((" ")):::initial`);
  else if(n.type==='final') lines.push(`  ${id(n.id)}(((" "))):::final`);
  else if(n.type==='merge') lines.push(`  ${n.id}{" "}:::merge`);
  else if(n.type==='decision') lines.push(`  ${n.id}{"${label}"}:::decision`);
  else lines.push(`  ${n.id}("${label}"):::${n.status==='EXTERNAL'?'external':n.status==='DỰ KIẾN'?'planned':n.status==='MỘT PHẦN'?'partial':'action'}`);
 }
 for(const e of diagram.edges)lines.push(`  ${id(e.from)} -->${e.label?'|"'+e.label+'"|':''} ${id(e.to)}`);
 lines.push('  classDef initial fill:#111,stroke:#111,color:#111;',
  '  classDef final fill:#111,stroke:#111,color:#111;',
  '  classDef action fill:#7ed0eb,stroke:#45525e,color:#162b3b;',
  '  classDef decision fill:#7ed0eb,stroke:#45525e,color:#162b3b;',
  '  classDef merge fill:#7ed0eb,stroke:#45525e,color:#162b3b;',
  '  classDef planned fill:#7ed0eb,stroke:#45525e,color:#162b3b,stroke-dasharray:6 4;',
  '  classDef external fill:#eeeeee,stroke:#666,color:#162b3b;',
  '  classDef partial fill:#7ed0eb,stroke:#b7791f,stroke-width:2px,color:#162b3b;');
 return lines.join('\n')+'\n';
}
function drawioPage(diagram){
 const nodes=diagram.nodes.map(geometry),map=new Map(nodes.map(n=>[n.id,n]));
 const cells=['<mxCell id="0"/><mxCell id="1" parent="0"/>'];
 if(diagram.targetScope) cells.push('<mxCell id="scope-note" value="Target scope: document preparation only. Backend changes pending. Ward visit is outside WardMate." style="text;whiteSpace=wrap;align=center;fontSize=14;" vertex="1" parent="1"><mxGeometry x="90" y="80" width="960" height="60" as="geometry"/></mxCell>');
 for(const n of nodes){
  let style='html=0;whiteSpace=wrap;fontFamily=Arial;fontSize=14;align=center;verticalAlign=middle;fillColor=#7ed0eb;strokeColor=#45525e;';
  if(n.type==='initial')style+='ellipse;fillColor=#111111;strokeColor=#111111;';
  else if(n.type==='final')style+='ellipse;fillColor=#ffffff;strokeColor=#111111;strokeWidth=2;';
  else if(n.type==='decision'||n.type==='merge')style+='rhombus;';
  else style+='rounded=1;arcSize=30;'+(n.status==='DỰ KIẾN'?'dashed=1;dashPattern=6 4;':n.status==='MỘT PHẦN'?'strokeColor=#b7791f;strokeWidth=2;':'');
  if(n.status==='EXTERNAL')style+='fillColor=#eeeeee;strokeColor=#666666;';
  const w=n.type==='final'?28:n.w,h=n.type==='final'?28:n.h;
  cells.push(`<mxCell id="${n.id}" value="${esc(n.text)}" style="${style}" vertex="1" parent="1"><mxGeometry x="${n.x-w/2}" y="${n.y-h/2}" width="${w}" height="${h}" as="geometry"/></mxCell>`);
  if(n.type==='final')cells.push(`<mxCell id="end-dot" value="" style="ellipse;fillColor=#111111;strokeColor=#111111;" vertex="1" parent="end"><mxGeometry x="6" y="6" width="16" height="16" as="geometry"/></mxCell>`);
 }
 diagram.edges.forEach((e,i)=>{
  const s=map.get(e.from),t=map.get(e.to),points=route(s,t,e.rail),first=points[0],last=points.at(-1);
  const ex=(first[0]-(s.x-s.w/2))/s.w,ey=(first[1]-(s.y-s.h/2))/s.h;
  const ix=(last[0]-(t.x-t.w/2))/t.w,iy=(last[1]-(t.y-t.h/2))/t.h;
  const style=`edgeStyle=orthogonalEdgeStyle;rounded=0;html=0;endArrow=open;endFill=0;strokeColor=#45525e;fontFamily=Arial;fontSize=12;labelBackgroundColor=#ffffff;exitX=${ex};exitY=${ey};entryX=${ix};entryY=${iy};`;
  cells.push(`<mxCell id="e${i}" value="${esc(e.label)}" style="${style}" edge="1" parent="1" source="${e.from}" target="${e.to}"><mxGeometry relative="1" as="geometry"><Array as="points">${points.slice(1,-1).map(p=>`<mxPoint x="${p[0]}" y="${p[1]}"/>`).join('')}</Array></mxGeometry></mxCell>`);
 });
 return `<diagram id="${diagram.id}" name="${esc(diagram.title)}"><mxGraphModel grid="1" gridSize="10" page="1" pageWidth="1140" pageHeight="${Math.max(...nodes.map(n=>n.y))+120}"><root>${cells.join('')}</root></mxGraphModel></diagram>`;
}
for(const diagram of diagrams){
 fs.writeFileSync(path.join(out,diagram.id+'.svg'),svgFor(diagram));
 fs.writeFileSync(path.join(out,diagram.id+'.mmd'),mermaidFor(diagram));
}
fs.writeFileSync(path.join(out,'wardmate-activities.drawio'),`<mxfile host="app.diagrams.net">${diagrams.map(drawioPage).join('')}</mxfile>`);
const html=`<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>WardMate — UML Activity Diagrams</title><style>body{margin:0;background:#edf2f6;color:#163047;font:16px system-ui}header,main{max-width:1200px;margin:auto;padding:24px}header h1{margin:0 0 12px}nav{display:flex;flex-wrap:wrap;gap:8px}a{color:#075984}nav a{background:white;border-radius:8px;padding:9px;text-decoration:none}section{background:white;margin:24px 0;padding:20px;border-radius:12px}img{display:block;width:100%;height:auto}.tools{display:flex;gap:20px}h2{font-size:20px}p{line-height:1.6}@media print{header,nav,.tools{display:none}section{break-before:page;margin:0;padding:0}img{max-height:260mm;object-fit:contain}body{background:white}@page{size:A3 portrait;margin:10mm}}</style><header><h1>WardMate · UML Activity Diagrams</h1><p>Concise English action labels. Black initial node, rounded blue actions, decision/merge diamonds, and a final node. Dashed actions are planned. Email integration remains partial. These diagrams describe target workflows, not a deployment verification. Pages 03, 04 and 08 follow the revised document-preparation scope; backend changes are pending. Visiting the ward office is outside WardMate.</p><p><a href="wardmate-activities.drawio" download>Download editable draw.io file — 9 pages</a> · <a href="README.md">Implementation notes</a></p><nav>${diagrams.map(d=>`<a href="#${d.id}">${esc(d.title)}</a>`).join('')}</nav><p>Works offline. Open an SVG to zoom, copy Mermaid source, or import the draw.io file to edit individual shapes.</p></header><main>${diagrams.map(d=>`<section id="${d.id}"><h2>${esc(d.title)}</h2><p class="tools"><a href="${d.id}.svg" target="_blank">Open SVG</a><a href="${d.id}.png" download>Download PNG</a><a href="${d.id}.mmd">Mermaid source</a></p><img src="${d.id}.svg" alt="${esc(d.title)}" loading="lazy"></section>`).join('')}</main></html>`;
fs.writeFileSync(path.join(out,'index.html'),html);
console.log(`Generated ${diagrams.length} SVG and Mermaid diagrams, editable draw.io pages, and offline HTML.`);
