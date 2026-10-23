const fs=require('fs'),path=require('path'); const out=__dirname;
const groups=[['IAM','IamDbContext'],['CAT','ProcedureDbContext'],['WF','WorkflowDbContext'],['DOC','DocumentDbContext'],['AI','ChatDbContext'],['SYS','NotificationDbContext'],['SYS','AnalyticsDbContext']];
const clean=s=>s.replaceAll('"','').trim(); const names=s=>s.split(',').map(clean);
const tables=[],schemas={};
for(const [group,ctx] of groups){
 const sql=fs.readFileSync(path.join(out,'schema',ctx+'.sql'),'utf8'); schemas[ctx]=sql;
 for(const match of sql.matchAll(/CREATE TABLE ([^\r\n(]+) \(\r?\n([\s\S]*?)\r?\n\);/g)){
  const table={group,ctx,sqlName:match[1].trim(),name:clean(match[1]).split('.').at(-1),cols:[],fks:[],pk:[],unique:[],constraints:[]};table.id=group+'_'+table.name;
  for(let line of match[2].split(/\r?\n/)){line=line.trim().replace(/,$/,'');if(line.startsWith('CONSTRAINT')){
   table.constraints.push(line);let m=line.match(/PRIMARY KEY \(([^)]+)\)/);if(m)table.pk=names(m[1]);
   m=line.match(/FOREIGN KEY \(([^)]+)\) REFERENCES ([^ ]+) \(([^)]+)\)(.*)/);if(m)table.fks.push({cols:names(m[1]),target:clean(m[2]),targetCols:names(m[3]),action:m[4].trim()});
   m=line.match(/UNIQUE \(([^)]+)\)/);if(m)table.unique.push(names(m[1]));
  }else{const m=line.match(/^("[^"]+"|\w+) (.*)$/);if(m){const spec=m[2],type=spec.split(/ NOT NULL| DEFAULT| GENERATED/)[0];table.cols.push({name:clean(m[1]),spec,type,nullable:!spec.includes('NOT NULL')&&!spec.includes('GENERATED')});}}
  }
  for(const idx of sql.matchAll(/CREATE (UNIQUE )?INDEX (\S+) ON (\S+) \(([^;]+)\);/g))if(idx[3]===table.sqlName&&idx[1])table.unique.push(names(idx[4]));
  tables.push(table);
 }
}
function target(t,f){return tables.find(x=>x.ctx===t.ctx&&clean(x.sqlName)===f.target);}
const edges=[];
for(const t of tables)for(const f of t.fks){const parent=target(t,f);if(!parent)throw Error('Missing FK target '+f.target);const nullable=f.cols.some(c=>t.cols.find(x=>x.name===c)?.nullable);const unique=[t.pk,...t.unique].some(k=>k.length===f.cols.length&&k.every(c=>f.cols.includes(c)));edges.push({from:parent.id,to:t.id,left:nullable?'|o':'||',right:unique?'o|':'o{',label:'FK '+f.cols.join('+'),f});}
const refs=[
 ['IAM_users','CAT_procedure_drafts','CreatedBy/ReviewedBy'],['CAT_procedures','CAT_procedure_drafts','PublishedProcedureId'],
 ['IAM_users','WF_applications','UserId/AssignedOfficerId'],['IAM_wards','WF_applications','WardCode'],['CAT_procedures','WF_applications','ProcedureId'],
 ['IAM_users','WF_application_comments','OfficerId'],['IAM_users','WF_application_versions','SubmittedBy'],['IAM_users','WF_application_status_history','ChangedBy'],
 ['WF_applications','WF_workflow_outbox','Payload.ApplicationId'],['DOC_form_templates','WF_application_checklists','FormTemplateId'],
 ['CAT_procedures','DOC_form_templates','FormDefinitions JSON'],['IAM_users','DOC_user_submissions','ApplicantId'],['DOC_form_templates','DOC_user_submissions','TemplateId'],
 ['WF_applications','DOC_user_submissions','ApplicationId'],['WF_application_checklists','DOC_user_submissions','ChecklistId'],
 ['WF_applications','DOC_application_forms','ApplicationId'],['WF_applications','DOC_supporting_documents','ApplicationId'],['WF_application_checklists','DOC_supporting_documents','ChecklistId'],
 ['WF_applications','DOC_generated_documents','ApplicationId'],['WF_applications','DOC_approved_qrs','ApplicationId unique'],['DOC_user_submissions','DOC_approved_qrs','SubmissionId'],
 ['IAM_users','AI_chat_sessions','UserId'],['IAM_wards','AI_chat_sessions','WardCode'],
 ['IAM_users','SYS_notifications','RecipientUserId'],['IAM_wards','SYS_notifications','RecipientWardCode'],['WF_applications','SYS_notifications','ApplicationId'],
 ['WF_workflow_outbox','SYS_notification_inbox','EventId'],['SYS_notification_inbox','SYS_notifications','SourceEventId'],
 ['WF_applications','SYS_citizen_feedback','ApplicationId unique'],['IAM_users','SYS_citizen_feedback','CitizenId'],['CAT_procedures','SYS_citizen_feedback','ProcedureId'],
 ['CAT_procedures','SYS_application_procedure_metrics','ProcedureId'],['IAM_users','SYS_daily_performance_metrics','OfficerId'],['CAT_procedures','SYS_daily_performance_metrics','ProcedureId'],
 ['IAM_wards','SYS_application_procedure_metrics','WardCode'],['IAM_wards','SYS_daily_performance_metrics','WardCode'],['IAM_wards','SYS_citizen_feedback','WardCode'],
 ['IAM_users','IAM_rbac_audit_logs','ActorUserId/TargetUserId'],['IAM_roles','IAM_rbac_audit_logs','RoleId'],['IAM_permissions','IAM_rbac_audit_logs','PermissionId']
];
for(const [a,b]of refs)if(!tables.some(t=>t.id===a)||!tables.some(t=>t.id===b))throw Error('Unknown REF '+a+' '+b);
const edgeText=e=>`    ${e.from} ${e.left}--${e.right} ${e.to} : "${e.label}"`;
const sanitize=s=>s.replace(/"/g,"'").replace(/[\r\n]/g,' ');
function entity(t,physical){let cols=t.cols;if(!physical)cols=cols.filter(c=>t.pk.includes(c.name)||t.fks.some(f=>f.cols.includes(c.name))||/id$|Id$|status|Status|security_stamp|verified_phone|deadline|receipt|documents_ready|TargetPhone/i.test(c.name));return [`    ${t.id} {`,...cols.map(c=>{
 const keys=[];if(t.pk.includes(c.name))keys.push('PK');if(t.fks.some(f=>f.cols.includes(c.name)))keys.push('FK');if(t.unique.some(k=>k.length===1&&k[0]===c.name))keys.push('UK');
 const type=physical?c.type.replaceAll(' ','_').replaceAll('[]','_array').replaceAll(',','_'):'value';
 return `        ${type} ${c.name}${keys.length?' '+keys.join(','):''} "${sanitize(physical?c.spec+(c.nullable?' NULL':''):c.nullable?'optional':'required')}"`;
 }), '    }'].join('\n');}
let logical=['erDiagram',...edges.map(edgeText),...refs.map(([a,b,label])=>`    ${a} ${label.includes("/") || label.includes("JSON") ? "}o..o{" : label.includes("unique") || label === "EventId" ? "|o..o|" : "|o..o{"} ${b} : "REF ${label}"`),...tables.map(t=>entity(t,false))].join('\n');fs.writeFileSync(path.join(out,'logical.mmd'),logical+'\n');
let physical='# WardMate — Physical ERD\n\nĐối chiếu EF model Release ngày 23/10/2026 (UTC+07:00), không đọc DB Azure. Có '+tables.length+' bảng ứng dụng trong 7 DbContext / 6 service. Không đếm bảng EF history, Hangfire hoặc hạ tầng broker. Alias IAM/CAT/WF/DOC/AI/SYS là nhãn sơ đồ, không phải schema.\n\nMỗi sơ đồ chỉ vẽ FK vật lý trong cùng DbContext. Cột giữ đúng casing PostgreSQL; PascalCase phải dùng dấu ngoặc kép khi SQL. Comment giữ kiểu SQL/nullable/default. Kiểu có khoảng trắng đổi thành underscore trong Mermaid. PK ghép được đánh dấu ở từng cột; UNIQUE ghép nằm trong bảng ràng buộc bên dưới. DDL [schema/](diagrams/erd/schema/) do EF dbcontext script sinh chỉ để đối chiếu mô hình, **không phải migration nâng cấp, không chạy trên database hiện hữu**.\n\n';
let blocks=[];
for(const [g,ctx]of groups){const ts=tables.filter(t=>t.ctx===ctx),ids=new Set(ts.map(t=>t.id));const mmd=['erDiagram',...edges.filter(e=>ids.has(e.to)).map(edgeText),...ts.map(t=>entity(t,true))].join('\n');fs.writeFileSync(path.join(out,'physical-'+ctx+'.mmd'),mmd+'\n');blocks.push({ctx,mmd});physical+=`## ${g} — ${ctx} (${ts.length} bảng)\n\n`+'~~~mermaid\n'+mmd+'\n~~~\n\n| Alias | Bảng PostgreSQL | PK | UNIQUE (kể cả ghép) |\n|---|---|---|---|\n'+ts.map(t=>`| ${t.id} | ${t.sqlName.includes('.')?t.sqlName:'public.'+t.sqlName} | ${t.pk.join(', ')} | ${t.unique.map(k=>k.join(' + ')).join('; ')||'—'} |`).join('\n')+`\n\nDDL đầy đủ, gồm CHECK, index, FK và ON DELETE: [${ctx}.sql](diagrams/erd/schema/${ctx}.sql).\n\n`;}
fs.writeFileSync('docs/system-erd-physical.md',physical);
fs.writeFileSync(path.join(out,'model-inventory.json'),JSON.stringify({date:'2026-10-23',tables,edges,refs},null,2));
const concept=`erDiagram
    USER }o--o{ ROLE : holds
    ROLE }o--o{ PERMISSION : grants
    WARD ||--o{ USER : locates
    USER ||--o{ AUTH_CHALLENGE : verifies
    USER ||--o| WELCOME_MESSAGE : receives
    CATEGORY ||--o{ PROCEDURE : classifies
    PROCEDURE ||--o{ PROCEDURE_VERSION : versions
    PROCEDURE ||--o{ PROCEDURE_DRAFT : prepares
    USER ||--o{ APPLICATION : creates
    WARD ||--o{ APPLICATION : receives
    PROCEDURE ||--o{ APPLICATION : defines
    APPLICATION ||--o{ CHECKLIST_ITEM : requires
    APPLICATION ||--o{ REVIEW_VERSION : snapshots
    REVIEW_VERSION ||--o{ REVIEW_COMMENT : annotates
    APPLICATION ||--o{ STATUS_HISTORY : records
    APPLICATION ||--o{ DOCUMENT : contains
    FORM_TEMPLATE ||--o{ FILLED_FORM : produces
    CHECKLIST_ITEM ||--o{ DOCUMENT : fulfilled_by
    APPLICATION ||--o| QR_REFERENCE : identifies
    APPLICATION ||--o| CITIZEN_FEEDBACK : receives
    USER ||--o{ CHAT_SESSION : starts
    CHAT_SESSION ||--o{ CHAT_MESSAGE : contains
    APPLICATION ||--o{ NOTIFICATION : triggers
    USER ||--o{ NOTIFICATION : receives
    NOTIFICATION ||--o{ DELIVERY : dispatches
    APPLICATION }o--o{ PERFORMANCE_METRIC : aggregates
`;
fs.writeFileSync(path.join(out,'conceptual.mmd'),concept);
fs.writeFileSync('docs/system-erd-conceptual.md','# WardMate — Conceptual ERD\n\nCập nhật 23/10/2026. Mô hình khái niệm của cả6service, tên tiếng Anh. Quan hệ ở đây thể hiện ý nghĩa nghiệp vụ, không phải FK SQL; không mô tả cột hoặc bảng kỹ thuật. APPLICATION là hồ sơ chuẩn bị/kiểm tra giấy tờ và ghi nhận tiếp nhận, không phải quyết định phê duyệt hành chính. AUTH_CHALLENGE gồm OTP mật khẩu/xác minh số; DOCUMENT gom metadata tài liệu, không có nghĩa một bảng duy nhất.\n\n~~~mermaid\n'+concept+'~~~\n\nXem [Logical](system-unified-erd.md) để tách đủ bảng, [Physical](system-erd-physical.md) để thấy tên cột, schema và FK thực tế. Các mục tương lai nằm riêng ở Logical, không trộn vào phần đã triển khai.\n');
let future=`erDiagram
    FUTURE_knowledge_documents ||--o{ FUTURE_knowledge_chunks : proposed_FK
    AI_chat_sessions ||..o{ FUTURE_ai_request_logs : proposed_REF
    AI_chat_messages }o..o{ FUTURE_knowledge_chunks : proposed_REF
    IAM_users ||..o{ FUTURE_ocr_scan_logs : proposed_REF
    IAM_users ||..o{ FUTURE_audit_logs : proposed_REF
    IAM_users ||..o{ FUTURE_system_settings : proposed_REF
    FUTURE_service_health_logs {
        uuid Id PK
    }
    FUTURE_system_backup_logs {
        uuid Id PK
    }
`;
fs.writeFileSync(path.join(out,'future.mmd'),future);
fs.writeFileSync('docs/system-unified-erd.md',`# WardMate — Logical ERD hợp nhất 6 service

Cập nhật 23/10/2026 từ 7 EF DbContext Release: **${tables.length} bảng ứng dụng đã có trong source**. Đây không phải xác nhận database/revision Azure đã được cập nhật.

- [Conceptual ERD](system-erd-conceptual.md): thực thể nghiệp vụ.
- [Physical ERD](system-erd-physical.md): đầy đủ cột và ràng buộc từ EF model.
- [Mermaid để nhập draw.io](diagrams/erd/logical.mmd): Arrange → Insert → Advanced → Mermaid.
- [Bản ERD cũ giữ để đối chiếu](diagrams/erd/archive-unified-20261015.md).

## Thay đổi so với bản cũ

| Service | Bảng hiện có | Thay đổi quan trọng |
|---|---:|---|
| IAM | 11 | Thêm iam.user_otps, iam.welcome_emails; users có security_stamp/verified_phone_number; OTP có target_phone_number |
| ProcedureCatalog | 4 | Cases/checklist/form definitions vẫn JSONB, không tạo bảng riêng |
| ApplicationWorkflow | 6 | applications thêm deadline/pause history JSONB, readiness, receipt và appointment; không tạo bảng deadline riêng |
| DocumentForm | 7 | document.approved_qrs đã có trong code; tên legacy, không mang nghĩa hệ thống phê duyệt hành chính |
| AIOCR | 2 | chat_sessions/chat_messages đã triển khai, không còn FUTURE |
| AnalyticsSystem | 7 | 3 bảng thông báo + 4 bảng analytics (metrics, feedback, refresh) |

Bản cũ đếm28 bảng đãcó +14 tươnglai; đối chiếu lần này có37 bảng đãcó (+9 trong tậpđãtriểnkhai:2IAM,2chat,4analytics,1QR), còn8 bảng thiết kế tươnglai. Đây là chênh lệch tài liệu, không nói cả9bảng được tạo ở lượt này. TASK25/27 LISTEN/NOTIFY, workerquota/cleanup và security_stamp không tạo thêm bảng realtime/session/quota.

## Quy ước

FK nét liền lấy từ EF, cardinality theo nullable/unique. REF nét đứt là tham chiếu nghiệp vụ, không FK xuyên database. REF biểu diễn đích tham chiếu có thể không tồn tại ở database hiện tại; đây là cardinality kỳ vọng theo ID, không được FK bảo đảm. Các cạnh gộp nhiều vai trò hoặc JSON dùng nhiều-nhiều; cột nullable/unique xem Physical. Một số nhóm CreatedBy/UpdatedBy và ID trongJSON khôngvẽ hết để hình dễđọc. Alias khôngphải schema. users.AssignedCategories là arraylogical; ChecklistSchema/FormDefinitions làJSON, không chuẩn hóa thành bảng giả. Outbox/Inbox liên hệEventId nhưng retention độc lập.

~~~mermaid
${logical}
~~~

## Phần tương lai — chưa có entity/migration

8 bảng: knowledge_documents,knowledge_chunks,ai_request_logs,ocr_scan_logs,audit_logs,system_settings,service_health_logs,system_backup_logs. FK/REF trong hình dưới chỉ đềxuất. Chat đãcó khôngđồngnghĩa vectorRAG đãcó. AuditRBAC đãcó riêng, khôngđồngnghĩa audittoànhệthống.

~~~mermaid
${future}
~~~

## Lưu ý vật lý và nghiệp vụ

- approved_at và các trạng thái APPROVED/REJECTED legacy vẫn tồn tại trong schema Workflow để tương thích dữliệucũ; luồngđích dùng DOCUMENTS_READY/OFFICIALLY_RECEIVED, không duyệthànhchính.
- documents_ready_at là tên SQL thực, tươngứng thờiđiểm readiness trongDTO; deadline_history/policy_snapshot làJSONB.
- Deadline analytics được tổnghợp quaAPI, không tự giảđịnh có bảng/chỉ sốpersisted mới: Physical thể hiện đúng cột hiện có.
- Document approved_qrs.SubmissionId và ApplicationId làREF, không vẽ FKgiả; application_id có unique.
- PhysicalSQL là GenerateCreateScript của model, khác migration ALTER nângcấp và không gồm EFhistory/Hangfire hạtầng.
`);
let old=fs.readFileSync('docs/system-diagrams.md','utf8');let a=old.indexOf('## 1. ERD'),b=old.indexOf('## 2.',a);if(a<0||b<0)throw Error('Missing section');let intro='# WardMate — Sơ đồ phân tích hệ thống\n\nERD đã cập nhật 23/10/2026: [Conceptual](system-erd-conceptual.md), [Logical hợp nhất](system-unified-erd.md), [Physical đầy đủ](system-erd-physical.md). Có37bảng EFứngdụng của6service;8bảng tươnglai táchriêng. Chỉ đối chiếu source, không xác nhậnAzure.\n\nCác mục Activity/State/Structure bên dưới giữ nguyên từbộtrước, không thuộc lượt cậpnhậtERD này; không dùng thôngtin lịchsử ở đó để phủđịnh ERD mới. [Bản cũ đầyđủ](diagrams/erd/archive-system-diagrams-20261015.md).\n\n';
let section='## 1. ERD — mô hình dữ liệu hiện tại\n\nĐây là7sơđồ vật lý theoDbContext của6service. Tấtcảcột lấy từEFmodel; FK nội bộ nétliền. [Các bảng tênSQL/PK/unique vàDDL](system-erd-physical.md). [Liên kết xuyênservice](system-unified-erd.md) chỉlàREF.\n\n'+blocks.map(x=>'### '+x.ctx+'\n\n~~~mermaid\n'+x.mmd+'\n~~~\n').join('\n');
// Keep activity/state/structure unchanged; clearly mark the old future section as historical.
let tail=old.slice(b).replace('## 6.', '> Mục6 là thiết kế lịch sử. Trạng thái bảng hiện tại và8bảng FUTURE đúng xem LogicalERD ngày23/10/2026.\n\n## 6.');const futureStart = tail.indexOf('## 6.');
if (futureStart >= 0) tail = tail.slice(0, futureStart) + '## 6. ERD tương lai\n\n8 bảng chưa triển khai được giữ riêng trong [Logical ERD](system-unified-erd.md). Chat, metrics, feedback và QR đã có, không còn thuộc phần tương lai. Xem bản lưu lịch sử nếu cần thiết kế trước đây.\n\n## 7. Activity, State Machine và Structure Analysis\n\nXem [bộ sơ đồ luồng](system-future-flows.md); phần này không thay đổi trong lượt cập nhật ERD.\n\n## 8. ERD hợp nhất\n\n[37 bảng hiện có và 8 bảng tương lai](system-unified-erd.md); FK vật lý và REF logic được phân biệt.\n';
fs.writeFileSync('docs/system-diagrams.md',intro+section+'\n'+tail);
console.log(JSON.stringify({tables:tables.length,columns:tables.reduce((n,t)=>n+t.cols.length,0),foreignKeys:edges.length,refs:refs.length,groups:groups.map(([g,ctx])=>[ctx,tables.filter(t=>t.ctx===ctx).length])}));

