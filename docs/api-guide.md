# WardMate — Chức năng và hướng dẫn sử dụng API theo Service

Cập nhật ngày **07/10/2026**. Tài liệu chia theo **Service → Nhóm chức năng → Từng API**, mô tả các
API nghiệp vụ hiện có, các endpoint hệ thống và đường dẫn Gateway. Không phải xác nhận tình trạng deploy.

**Cách đọc:** Mỗi API có mục đích, quyền, cách dùng, kết quả và lỗi thường gặp trên các dòng riêng. Public
nghĩa là không cần đăng nhập. Bearer là access token lấy sau đăng nhập. Thay `{id}` bằng ID thật từ API danh
sách, không thay bằng tên người hay tên thủ tục.

**Lỗi chung:** API có bảo vệ trả 401 khi token không hợp lệ (refresh hoặc đăng nhập lại), 403 khi thiếu quyền
(kiểm tra/cấp đúng quyền). 200/201 là thành công, 202 là đã nhận để xử lý, 204 là thành công không có JSON
body. Các mục lỗi tập trung tình huống riêng của từng API, không liệt kê mọi lỗi hạ tầng có thể xảy ra.

**Tra cứu:** 1. IAM · 2. Procedure Catalog · 3. Document & Form · 4. AIOCR · 5. Gateway và hệ thống. Phần
**Phụ lục kỹ thuật** cuối file giữ các cổng, DTO, ví dụ JSON/fetch và cấu hình cần thiết. Các dẫn chiếu “mục
3.1/4.3/6.1” trong hướng dẫn là mục thuộc phụ lục.

## 1. IAM Service (Identity & Access Management)

**Chịu trách nhiệm:** Đăng ký, đăng nhập, quản lý phiên, hồ sơ cá nhân, khóa/mở tài khoản và phân quyền RBAC.

### Nhóm 1.1 — Xác thực & Quản lý phiên (`/api/v1/auth`)

Yêu cầu chung: cả bốn POST đều cần `X-CSRF-Protection: 1`. FE dùng `credentials: "include"` cho cookie đăng
nhập/refresh/đăng xuất. Role mặc định khi đăng ký là **REGISTERED_CITIZEN**. Thời hạn token lấy từ
response/cấu hình, không hardcode số phút/ngày vào FE.

#### API 01 — Đăng ký tài khoản công dân

`POST /api/v1/auth/register`

**Quyền / Header:** Public + CSRF

**Mục đích:** Tạo tài khoản mới cho người chưa có tài khoản, đồng thời tạo hồ sơ tên người dùng và gán vai trò
công dân. Không dùng API này để tự tạo tài khoản quản trị.

**Cách dùng:** Không cần đăng nhập. Gửi `username`, `email`, `password`, `fullName` và header
`X-CSRF-Protection: 1`. Ví dụ người dùng điền bốn ô trên màn hình Đăng ký rồi bấm gửi.

**Kết quả:** Nhận 201 và thông tin tài khoản vừa tạo. Sau đó gọi API 02 để đăng nhập; đăng ký chưa cấp phiên
đăng nhập.

**Trường hợp sai và cách xử lý:**

- Thiếu tên, email sai hoặc mật khẩu không đủ 8 ký tự/chữ hoa/ký tự đặc biệt → 400, sửa trường được báo.
- Trùng username/email → 409, dùng thông tin khác hoặc đăng nhập tài khoản đã có.
- Thiếu header CSRF hoặc Origin không được phép → 403, sửa cách gọi/cấu hình, không phải đổi mật khẩu.

#### API 02 — Đăng nhập

`POST /api/v1/auth/login`

**Quyền / Header:** Public + CSRF

**Mục đích:** Mở phiên làm việc để sử dụng các chức năng cần quyền.

**Cách dùng:** Gửi `usernameOrEmail` và `password`, kèm `X-CSRF-Protection: 1`. FE phải bật
`credentials: 'include'` để trình duyệt nhận cookie. Không cần Bearer trước khi đăng nhập.

**Kết quả:** Nhận 200, accessToken và thời gian hết hạn. Trình duyệt giữ refresh token trong cookie HttpOnly;
FE dùng accessToken cho các API cần đăng nhập.

**Trường hợp sai và cách xử lý:**

- Sai thông tin hoặc tài khoản không còn khả dụng → 401; kiểm tra thông tin/tình trạng tài khoản.
- 403 CSRF → kiểm tra header/Origin.
- Không thấy refreshToken trong JSON là bình thường; không cố đọc cookie HttpOnly bằng JavaScript.

#### API 03 — Gia hạn phiên đăng nhập

`POST /api/v1/auth/refresh-token`

**Quyền / Header:** Public + Cookie + CSRF

**Mục đích:** Lấy access token mới khi access token cũ hết hạn mà người dùng chưa cần nhập lại mật khẩu.

**Cách dùng:** Gọi từ cùng host đã đăng nhập, gửi cookie bằng `credentials: 'include'` và header CSRF. **Không
gửi body AccessToken/RefreshToken**. FE chỉ cho một yêu cầu refresh chạy tại một thời điểm.

**Kết quả:** Nhận 200 với access token mới; cookie refresh cũ được thay bằng cookie mới. Dùng token mới để thử
lại yêu cầu trước đó một lần.

**Trường hợp sai và cách xử lý:**

- Không có cookie, cookie hết hạn, đã dùng hoặc bị thu hồi → 401, yêu cầu đăng nhập lại.
- Đăng nhập localhost rồi refresh bằng 127.0.0.1 có thể mất cookie; dùng nhất quán một hostname.
- Gửi nhiều lần đồng thời có thể làm một lần thất bại vì token chỉ được dùng một lần.

#### API 04 — Đăng xuất phiên hiện tại

`POST /api/v1/auth/revoke-token`

**Quyền / Header:** Bearer + Cookie + CSRF

**Mục đích:** Hủy khả năng gia hạn của phiên đang sử dụng khi người dùng bấm Đăng xuất.

**Cách dùng:** Gửi Bearer, header CSRF và cookie; không body. Sau khi xử lý, FE xóa access token đang giữ và
đưa người dùng về màn hình đăng nhập.

**Kết quả:** Nhận 204, cookie được xóa. Không có cookie refresh vẫn có thể nhận 204; không cần xem đây là lỗi.

**Trường hợp sai và cách xử lý:**

- Access token không hợp lệ → 401; thiếu header/Origin sai → 403.
- Chỉ xóa token trên FE mà không gọi API sẽ không thu hồi refresh trên server.
- API này không phải nút đăng xuất mọi thiết bị và không đảm bảo access token đã phát hành lập tức mất hiệu
  lực tại mọi service.


### Nhóm 1.2 — Người dùng & Hồ sơ cá nhân (`/api/v1/users`)

Hồ sơ gồm họ tên, số định danh, điện thoại, ngày sinh, giới tính, địa chỉ thường trú và tạm trú. Đăng ký đã
tạo hồ sơ; sửa bằng PUT, không POST lại. Xóa hồ sơ không xóa tài khoản.

#### API 05 — Xem tôi là ai và có quyền gì

`GET /api/v1/users/me`

**Quyền / Header:** Bearer + `iam.profile.read`

**Mục đích:** Hiển thị người đang đăng nhập, hồ sơ, vai trò và quyền; FE dùng để dựng menu phù hợp.

**Cách dùng:** Gửi Bearer của chính người dùng có `iam.profile.read`, không cần userId hay body.

**Kết quả:** Nhận 200 với tài khoản, profile nếu có, danh sách roles/permissions.

**Trường hợp sai và cách xử lý:**

- Dùng token người khác sẽ nhận thông tin của người đó, không phải người đang hiện trên giao diện.
- Token sai → 401; thiếu quyền → 403.
- Profile không có sau khi xóa hồ sơ không đồng nghĩa tài khoản đã bị xóa.

#### API 06 — Xem hồ sơ của mình

`GET /api/v1/users/me/profile`

**Quyền / Header:** Bearer + `iam.profile.read`

**Mục đích:** Điền dữ liệu hiện tại vào màn hình Hồ sơ cá nhân.

**Cách dùng:** Gửi Bearer có `iam.profile.read`, không body; hệ thống tự nhận biết người dùng từ token.

**Kết quả:** Nhận 200 với dữ liệu hồ sơ, không phải toàn bộ thông tin vai trò như API 05.

**Trường hợp sai và cách xử lý:**

- 404 nghĩa là chưa có/đã xóa hồ sơ — chuyển sang tạo hồ sơ bằng API 07.
- Không gọi lại GET mãi để chờ dữ liệu tự xuất hiện.

#### API 07 — Tạo lại hồ sơ của mình khi chưa có

`POST /api/v1/users/me/profile`

**Quyền / Header:** Bearer + `iam.profile.write`

**Mục đích:** Tạo hồ sơ cho tài khoản hiện chưa có hồ sơ.

**Cách dùng:** Gửi Bearer có `iam.profile.write` và các trường hồ sơ; fullName bắt buộc.

**Kết quả:** Nhận 201 và hồ sơ mới.

**Trường hợp sai và cách xử lý:**

- Sau đăng ký đã có hồ sơ, gọi POST sẽ nhận 409; muốn sửa thì dùng API 08.
- Dữ liệu sai như ngày sinh tương lai → 400.
- Số định danh trùng người khác → xung đột; kiểm tra lại, không đổi ngẫu nhiên để vượt kiểm tra.

#### API 08 — Sửa hồ sơ của mình

`PUT /api/v1/users/me/profile`

**Quyền / Header:** Bearer + `iam.profile.write`

**Mục đích:** Lưu thông tin cá nhân sau khi chỉnh sửa.

**Cách dùng:** Có `iam.profile.write`; lấy hồ sơ bằng API 06, chỉnh rồi gửi toàn bộ trường muốn giữ lại.

**Kết quả:** Nhận 200 với hồ sơ đã cập nhật.

**Trường hợp sai và cách xử lý:**

- Chỉ gửi tên và bỏ địa chỉ có thể làm địa chỉ cũ bị xóa về null: đây là thay cả hồ sơ, không phải chỉ sửa các
  trường gửi lên.
- 404 → tạo hồ sơ trước; 400 → sửa trường sai; 409 → kiểm tra trùng số định danh hoặc tải lại nếu hồ sơ đã
  thay đổi.

#### API 09 — Xóa hồ sơ của mình

`DELETE /api/v1/users/me/profile`

**Quyền / Header:** Bearer + `iam.profile.write`

**Mục đích:** Gỡ thông tin hồ sơ cá nhân, không xóa tài khoản đăng nhập.

**Cách dùng:** Có `iam.profile.write`, gọi không body sau khi người dùng chọn xóa.

**Kết quả:** Nhận 204; lần xem hồ sơ sau có thể trả 404. Có thể tạo lại bằng API 07.

**Trường hợp sai và cách xử lý:**

- Dùng API này với ý định khóa/xóa tài khoản sẽ không đạt mục đích.
- Nếu hồ sơ không còn tồn tại, xử lý phản hồi không tìm thấy thay vì thông báo hệ thống hỏng.


### Nhóm 1.3 — Quản trị tài khoản & Hồ sơ người dùng

Dùng cho IT_ADMIN có iam.manage hoặc MANAGER đã được gán phường. Tài khoản quản lý việc đăng nhập; hồ sơ quản lý thông tin cá nhân. Chọn
đúng userId trước khi thao tác.

#### API 14 — Danh sách tài khoản

`GET /api/v1/accounts`

**Quyền / Header:** Bearer: Admin toàn hệ thống / Manager trong phường

**Mục đích:** Quản trị tìm tài khoản để xem trạng thái, sửa hồ sơ hoặc cấp vai trò.

**Cách dùng:** Trong phạm vi quản lý; gọi `?page=1&pageSize=20`. Dùng id trong từng dòng cho các API tài khoản/hồ
sơ/RBAC.

**Kết quả:** Nhận 200 gồm items và tổng số tài khoản `total`.

**Trường hợp sai và cách xử lý:**

- pageSize vượt 100 hoặc trang không hợp lệ → 400.
- Trang rỗng không có nghĩa toàn hệ thống không có tài khoản; kiểm tra page và total.
- API này chưa có tham số tìm theo tên được công bố.

#### API 15 — Xem một tài khoản

`GET /api/v1/accounts/{userId}`

**Quyền / Header:** Bearer: Admin toàn hệ thống / Manager trong phường

**Mục đích:** Xem username, email, trạng thái hoạt động và ngày tạo trước khi quản trị thao tác.

**Cách dùng:** Trong phạm vi quản lý, điền id lấy từ API 14; không body.

**Kết quả:** Nhận 200 với thông tin tài khoản.

**Trường hợp sai và cách xử lý:**

- userId không tồn tại → 404, tải lại danh sách.
- Không tìm họ tên/địa chỉ trong response này; dùng API 10 để xem hồ sơ.

#### API 16 — Khóa hoặc mở lại tài khoản

`PUT /api/v1/accounts/{userId}/status`

**Quyền / Header:** Bearer: Admin toàn hệ thống / Manager trong phường

**Mục đích:** Ngừng cho một tài khoản sử dụng hệ thống hoặc cho phép hoạt động trở lại.

**Cách dùng:** Trong phạm vi quản lý; gửi `{ "isActive": false }` để khóa, true để mở. Không có trường thời hạn
khóa tự động.

**Kết quả:** Nhận 204. Khi khóa, refresh token bị thu hồi; khi mở lại, người dùng đăng nhập lại để có phiên
mới.

**Trường hợp sai và cách xử lý:**

- Bỏ isActive → 400; tài khoản không có → 404; tự khóa mình hoặc khóa IT_ADMIN hoạt động cuối cùng → 409.
- Không dùng thao tác này thay cho xóa hồ sơ; và không khẳng định JWT đã cấp mất hiệu lực ngay ở mọi service
  chỉ kiểm tra JWT.

#### API 60 — Danh sách hồ sơ người dùng

**Method & Đường dẫn:** `GET /api/v1/users/profiles?page=1&pageSize=20`

**Mục đích:** Hiển thị danh sách thông tin cá nhân để quản lý mở/xem/sửa hồ sơ theo userId.
Khác danh sách tài khoản, API này chỉ trả hồ sơ đang tồn tại; người chưa có/đã xóa profile không xuất hiện.

**Ai sử dụng:** IT_ADMIN có quyền iam.manage xem toàn hệ thống. Manager đã được gán phường chỉ xem
Front Desk cùng phường theo phạm vi quản lý hiện hành; không xem công dân hoặc cán bộ phường khác.
Front Desk có role đặc quyền khác ngoài FRONT_DESK_OFFICER/REGISTERED_CITIZEN không thuộc phạm vi Manager.

**Cách gọi:** Gửi `Authorization: Bearer <accessToken>`, không body. page mặc định 1 (tối đa 1.000.000),
pageSize mặc định 20 (tối đa 100). Muốn lấy hết thì gọi lần lượt các trang đến khi đủ total.
Không truyền wardId/userId để tự mở rộng quyền. Gateway dùng cùng route tại cổng 5000; IAM local cổng 5001.

**Kết quả:** 200 với `{items, page, pageSize, total}`, sắp xếp fullName rồi userId để phân trang ổn định.
Mỗi item gồm userId, fullName, identityNumber, phoneNumber, dateOfBirth, gender, permanentAddress,
temporaryAddress, updatedAt. Trường null bị bỏ theo cấu hình JSON của IAM. updatedAt là UTC.
Lấy userId để gọi `GET /api/v1/users/{userId}/profile`; không nhầm profile với tài khoản đăng nhập.
Hồ sơ tài khoản bị khóa vẫn xuất hiện nếu thuộc phạm vi; total là số profile được phép xem, không phải số user.

**Lỗi và cách xử lý:**

- 400 khi page/pageSize sai hoặc không phải số → sửa query theo errors tiếng Việt.
- 401 khi token sai/hết hạn → refresh qua IAM hoặc đăng nhập lại.
- 403 khi không có quyền, Manager chưa có phường hoặc tài khoản quản lý bị khóa → nhờ Admin kiểm tra.
- items rỗng vẫn trả 200; kiểm tra trang và hồ sơ trong phạm vi, không xem đây là lỗi hệ thống.

#### API 10 — Quản trị xem hồ sơ một người

`GET /api/v1/users/{userId}/profile`

**Quyền / Header:** Bearer: Admin toàn hệ thống / Manager trong phường

**Mục đích:** Người quản lý xem hồ sơ tài khoản được chọn trong màn hình quản trị.

**Cách dùng:** Lấy userId từ danh sách tài khoản API 14, điền vào URL và gửi Bearer quản trị.

**Kết quả:** Nhận 200 với hồ sơ của đúng userId đã chọn.

**Trường hợp sai và cách xử lý:**

- Nhập username thay UUID không gọi được route hợp lệ.
- 404 → kiểm tra tài khoản hoặc hồ sơ có tồn tại; không tự coi đó là lỗi đăng nhập.

#### API 11 — Quản trị tạo hồ sơ cho một người

`POST /api/v1/users/{userId}/profile`

**Quyền / Header:** Bearer: Admin toàn hệ thống / Manager trong phường

**Mục đích:** Bổ sung hồ sơ còn thiếu cho một tài khoản đã tồn tại; không tạo tài khoản mới.

**Cách dùng:** Trong phạm vi quản lý, chọn đúng userId và gửi dữ liệu hồ sơ có fullName.

**Kết quả:** Nhận 201 với hồ sơ được tạo cho người đó.

**Trường hợp sai và cách xử lý:**

- userId không có → 404; đã có hồ sơ → 409, dùng API 12; dữ liệu sai → 400.
- Kiểm tra người được chọn để tránh tạo thông tin cho nhầm tài khoản.

#### API 12 — Quản trị sửa hồ sơ một người

`PUT /api/v1/users/{userId}/profile`

**Quyền / Header:** Bearer: Admin toàn hệ thống / Manager trong phường

**Mục đích:** Sửa thông tin hồ sơ cho tài khoản được quản trị chọn.

**Cách dùng:** Trong phạm vi quản lý; đọc bằng API 10 trước rồi gửi đầy đủ hồ sơ đã chỉnh.

**Kết quả:** Nhận 200 với dữ liệu mới.

**Trường hợp sai và cách xử lý:**

- Bỏ trường tùy chọn sẽ xóa giá trị cũ; không dùng PUT như thao tác sửa riêng một ô.
- 404 → kiểm tra user/hồ sơ; 400/409 → xem lỗi trường hoặc trùng số định danh và sửa lại.

#### API 13 — Quản trị xóa hồ sơ một người

`DELETE /api/v1/users/{userId}/profile`

**Quyền / Header:** Bearer: Admin toàn hệ thống / Manager trong phường

**Mục đích:** Xóa hồ sơ được chọn trong quản trị, giữ lại tài khoản.

**Cách dùng:** Trong phạm vi quản lý, kiểm tra userId rồi gọi không body.

**Kết quả:** Nhận 204; tài khoản không bị khóa bởi thao tác này.

**Trường hợp sai và cách xử lý:**

- Chọn nhầm userId sẽ tác động nhầm hồ sơ; cần xác nhận đúng người trên giao diện.
- Muốn ngăn người đó đăng nhập thì dùng API 16, không dùng xóa hồ sơ.



**Phạm vi bắt buộc:** Manager chỉ thấy/quản lý Front Desk cùng phường công tác, không phải
địa chỉ thường trú. Backend lấy phường từ database. Không có phường thì 403; user ngoài phạm
vi trả 404 ở API tài khoản/hồ sơ. Front Desk kiêm role quản trị/role tùy chỉnh được bảo vệ,
Manager không quản lý tài khoản đó. Admin quản lý toàn bộ, kể cả tài khoản chưa gán phường.

### Nhóm 1.3b — Danh sách người dùng, phường và tạo Front Desk

Admin là IT_ADMIN có iam.manage. Manager cần role MANAGER đang hoạt động và phường công tác
được Admin gán. Một phường có nhiều Front Desk. Không cấp iam.manage toàn cục cho Manager.

#### API 54 — Danh sách người dùng và thông tin hồ sơ

`GET /api/v1/users`

**Quyền:** Admin xem tất cả; Manager chỉ xem Front Desk cùng phường.

**Mục đích:** Dựng màn hình quản lý nhân sự có tài khoản, hồ sơ, phường và vai trò.
API accounts là bản gọn về tài khoản; API users này có thêm thông tin người dùng.

**Cách dùng:** Gọi `?page=1&pageSize=20`, pageSize từ 1 đến 100. Backend tự giới hạn phạm vi;
không nhận wardId để Manager tùy ý chọn xem phường khác. Gọi nhiều trang để lấy toàn bộ
người dùng trong phạm vi, không tải không giới hạn một lần.

**Kết quả:** 200 `{items,page,pageSize,total}`. Mỗi item gồm id, username, email, isActive,
wardId?, wardName?, profile? (cấu trúc hồ sơ ở mục 3.1), roles là mảng `{id,roleName}`.
Không trả passwordHash hoặc refresh token. total cũng chỉ tính trong phạm vi được phép.

**Trường hợp sai:** Phân trang sai → 400; thiếu quyền/chưa gán phường → 403.
Danh sách rỗng có thể do phường chưa có Front Desk. roles của API này là object có ID;
roles trong GET users/me vẫn là mảng tên. Dùng role ID từ dữ liệu, không đoán ID theo thứ tự.

#### API 55 — Tạo tài khoản Front Desk

`POST /api/v1/accounts/front-desk`

**Quyền:** Admin hoặc Manager trong phường.

**Mục đích:** Tạo tài khoản đăng nhập, hồ sơ và role FRONT_DESK_OFFICER trong một giao dịch.
Manager không phải đăng ký tài khoản công dân rồi nhờ Admin nâng quyền cho từng cán bộ.

**Cách dùng:** Gửi username, email, password, fullName, wardId?. Manager bỏ wardId để server
tự lấy phường của mình; Admin phải chọn wardId hợp lệ. Quy tắc mật khẩu giống đăng ký:
ít nhất 8 ký tự, có chữ hoa và ký tự đặc biệt, tối đa 72 byte UTF-8.

**Kết quả:** 201 ManagedUserDto giống một item của API 54, Location tới accounts/{id}.
Tài khoản mới active, có hồ sơ và role Front Desk; không tự đăng nhập hoặc trả token.

**Trường hợp sai:** Dữ liệu sai → 400; username/email trùng → 409; Manager truyền phường
khác → 403; Admin không chọn phường hoặc phường không tồn tại → 404. Không có trường cho
Manager tùy ý chọn role quản trị trong body. Không ghi mật khẩu thực vào tài liệu/mã FE.

#### API 56 — Xem danh sách phường công tác

`GET /api/v1/accounts/wards`

**Quyền:** Admin hoặc Manager đã có phường.

**Mục đích:** Lấy ID phường cho màn hình tạo cán bộ/gán phường.

**Cách dùng:** Gửi Bearer, không body. Admin nhận toàn bộ phường; Manager chỉ nhận phường mình.

**Kết quả:** 200 mảng `{id,code,name}`. Đây là phường công tác, không suy ra từ địa chỉ hồ sơ.

**Trường hợp sai:** 403 nếu Manager chưa có phường. Admin nhận mảng rỗng thì tạo phường trước.

#### API 57 — Admin tạo phường

`POST /api/v1/accounts/wards`

**Quyền:** Chỉ Admin.

**Mục đích:** Khởi tạo đơn vị quản lý để gán Manager và Front Desk.

**Cách dùng:** Gửi ví dụ `{ "code": "DEMO_A", "name": "Phường minh họa A" }`.
Code tối đa 50, chỉ chữ ASCII/số/gạch ngang/gạch dưới, chuẩn hóa chữ hoa; name tối đa 255.

**Kết quả:** 201 `{id,code,name}`. Giữ ID để gán tài khoản ở API 58.

**Trường hợp sai:** Thiếu/sai trường → 400; mã trùng → 409; Manager gọi → 403.
Tạo phường không tự tạo tài khoản Manager.

#### API 58 — Admin gán hoặc chuyển phường công tác

`PUT /api/v1/accounts/{userId}/ward`

**Quyền:** Chỉ Admin. Manager không tự chuyển phường hoặc đưa người ngoài vào phường mình.

**Mục đích:** Xác lập phạm vi quản lý bằng quan hệ dữ liệu cụ thể.

**Cách dùng:** Gửi `{ "wardId": "<UUID từ API 56>" }`. Gửi wardId null để gỡ phường.
Body rỗng `{}` cũng có wardId null, vì vậy FE phải gửi rõ lựa chọn trước khi lưu.

**Kết quả:** 204, cập nhật phường và thu hồi refresh token của tài khoản được chuyển.
Phạm vi IAM được kiểm tra lại từ DB ngay cả với access token cũ; user/me trả wardId mới.

**Trường hợp sai:** User/phường không có → 404; Manager gọi → 403. Gỡ phường khỏi Manager
khiến Manager mất quyền quản lý cán bộ cho đến khi được gán lại. Không tự chuyển toàn bộ
Front Desk theo khi chuyển Manager; Admin phải xử lý từng tài khoản cần chuyển.

**Luồng sử dụng:** Admin tạo phường → gán role MANAGER bằng RBAC và gán wardId cho Manager
→ Manager tạo Front Desk (tự thuộc phường mình) → GET users/accounts để quản lý.
Manager có thể PUT/DELETE role FRONT_DESK_OFFICER của người cùng phường theo API 27/28;
không được cấp role khác. Catalog chỉ dùng lọc, không gắn cứng vào tài khoản.

### Nhóm 1.4 — Phân quyền vai trò RBAC (`/api/v1/rbac`)

RBAC Admin: tài khoản đang hoạt động có **IT_ADMIN và iam.manage**. Vai trò là nhóm người dùng; quyền là hành
động được phép. Tạo role mới không tự bổ sung chức năng backend hay vượt các policy cố định.

#### API 17 — Danh sách vai trò

`GET /api/v1/rbac/roles`

**Quyền / Header:** RBAC Admin

**Mục đích:** Hiển thị các vai trò để quản trị chọn khi cấp quyền.

**Cách dùng:** Gọi `?page=1&pageSize=20`, gửi Bearer quản trị.

**Kết quả:** Nhận 200 với danh sách role và tổng số. Giữ id, không dùng thứ tự dòng làm roleId.

**Trường hợp sai và cách xử lý:**

- Phân trang sai → 400; trang rỗng → kiểm tra trang/tổng.
- Không tự mặc định IT_ADMIN luôn là một số ID cố định trên mọi môi trường.

#### API 18 — Xem chi tiết vai trò

`GET /api/v1/rbac/roles/{roleId}`

**Quyền / Header:** RBAC Admin

**Mục đích:** Xem tên, mô tả, có phải vai trò hệ thống không và các quyền đang được gán.

**Cách dùng:** Điền roleId từ API 17, không body.

**Kết quả:** Nhận 200 với thông tin vai trò.

**Trường hợp sai và cách xử lý:**

- Không tồn tại → 404; truyền tên role thay số không đúng route. isSystem=true là dấu hiệu vai trò được bảo
  vệ, không phải được phép xóa.

#### API 19 — Tạo vai trò tùy chỉnh

`POST /api/v1/rbac/roles`

**Quyền / Header:** RBAC Admin

**Mục đích:** Tạo nhóm quyền mới khi hệ thống cần thêm nhóm người dùng.

**Cách dùng:** Gửi ví dụ `{ "roleName": "CUSTOM_OFFICER", "description": "Nhóm cán bộ bổ sung" }`, sau đó
dùng API 24 để gán quyền và API 27 để gán người.

**Kết quả:** Nhận 201 với vai trò mới. Chỉ tạo role không tự cấp nó cho ai và không tự thêm chức năng backend.

**Trường hợp sai và cách xử lý:**

- Tên có dấu/khoảng trắng, bắt đầu bằng số hoặc quá dài → 400; trùng tên → 409.
- Role tùy chỉnh không tự vượt policy cố định PROCEDURE_MANAGER/IT_ADMIN của Procedure.

#### API 20 — Sửa vai trò

`PUT /api/v1/rbac/roles/{roleId}`

**Quyền / Header:** RBAC Admin

**Mục đích:** Cập nhật tên/mô tả vai trò, không phải sửa danh sách quyền.

**Cách dùng:** Gửi roleName và description mới cho roleId đã chọn.

**Kết quả:** Nhận 200 với role đã sửa.

**Trường hợp sai và cách xử lý:**

- Role không có → 404; tên sai → 400; trùng tên hoặc đổi tên role hệ thống → 409.
- Muốn đổi quyền thì gọi API 24/25.
- Mô tả role hệ thống có thể chỉnh khi giữ nguyên tên được bảo vệ.

#### API 21 — Xóa vai trò tùy chỉnh

`DELETE /api/v1/rbac/roles/{roleId}`

**Quyền / Header:** RBAC Admin

**Mục đích:** Bỏ vai trò tùy chỉnh không còn dùng.

**Cách dùng:** Kiểm tra role không phải hệ thống và không còn được gán cho người dùng, rồi gọi không body.

**Kết quả:** Nhận 204; cập nhật lại danh sách role trên giao diện.

**Trường hợp sai và cách xử lý:**

- Role hệ thống hoặc còn người dùng được gán → 409; gỡ vai trò khỏi người dùng trước nếu thật sự cần xóa.
- Role không tồn tại → 404.
- Không xóa tài khoản người dùng để xử lý lỗi này.

#### API 22 — Xem danh sách quyền có sẵn

`GET /api/v1/rbac/permissions`

**Quyền / Header:** RBAC Admin

**Mục đích:** Lấy các quyền mà backend hiện biết để dựng danh sách chọn quyền.

**Cách dùng:** Gửi Bearer quản trị, không body, không cần roleId.

**Kết quả:** Nhận 200 với mã, tên, module và id của từng quyền.

**Trường hợp sai và cách xử lý:**

- Không thấy quyền mong muốn không có nghĩa gọi lỗi; quyền đó có thể chưa được triển khai/seed.
- Không tự gửi một permissionId đoán vào API gán quyền; lấy ID từ danh sách này.

#### API 23 — Xem quyền của một vai trò

`GET /api/v1/rbac/roles/{roleId}/permissions`

**Quyền / Header:** RBAC Admin

**Mục đích:** Đánh dấu các quyền role đang có trong màn hình phân quyền.

**Cách dùng:** Chọn role từ API 17, gọi với roleId, không body.

**Kết quả:** Nhận 200 với mảng quyền; mảng rỗng nghĩa role chưa có quyền.

**Trường hợp sai và cách xử lý:**

- Role không còn tồn tại → 404, tải lại danh sách.
- Không coi mảng rỗng là lý do cấp tất cả quyền tự động.

#### API 24 — Gán một quyền cho vai trò

`PUT /api/v1/rbac/roles/{roleId}/permissions/{permissionId}`

**Quyền / Header:** RBAC Admin

**Mục đích:** Cho người thuộc vai trò được hưởng thêm một quyền của IAM.

**Cách dùng:** Lấy roleId từ API 17, permissionId từ API 22; điền cả hai trong URL, không body.

**Kết quả:** Nhận 204; tải lại API 23 để thấy quyền vừa gán. Gán lại liên kết đã có không tạo quyền trùng.

**Trường hợp sai và cách xử lý:**

- Role/quyền không có → 404.
- Gán đúng quyền cho nhầm role vẫn có thể thành công và tác động nhiều người; kiểm tra tên role và quyền trước
  khi gửi.

#### API 25 — Gỡ một quyền khỏi vai trò

`DELETE /api/v1/rbac/roles/{roleId}/permissions/{permissionId}`

**Quyền / Header:** RBAC Admin

**Mục đích:** Ngừng cấp một quyền thông qua vai trò này; không xóa định nghĩa quyền.

**Cách dùng:** Chọn liên kết từ API 23, gọi không body.

**Kết quả:** Nhận 204. Người dùng còn role khác cấp cùng quyền thì vẫn có thể giữ quyền đó.

**Trường hợp sai và cách xử lý:**

- Không có role/quyền → 404; gỡ quyền quản trị hệ thống của IT_ADMIN → 409 vì được bảo vệ.
- Đừng kết luận gỡ thất bại chỉ vì một người vẫn còn quyền qua vai trò khác.

#### API 26 — Xem các vai trò của một tài khoản

`GET /api/v1/rbac/users/{userId}/roles`

**Quyền / Header:** RBAC Admin

**Mục đích:** Xem một người đang là công dân, cán bộ hay quản trị trước khi điều chỉnh.

**Cách dùng:** Lấy userId từ API 14 và gọi không body.

**Kết quả:** Nhận 200 với mảng role; một người có thể có nhiều role.

**Trường hợp sai và cách xử lý:**

- userId không tồn tại → 404.
- Không dùng email thay userId; không suy ra vai trò chỉ từ tên tài khoản.

#### API 27 — Gán vai trò cho tài khoản

`PUT /api/v1/rbac/users/{userId}/roles/{roleId}`

**Quyền / Header:** RBAC Admin hoặc Manager cấp/gỡ riêng FRONT_DESK_OFFICER cùng phường

**Mục đích:** Ví dụ cấp PROCEDURE_MANAGER cho người phụ trách thủ tục.

**Cách dùng:** Lấy hai ID từ API 14 và 17, gọi không body; sau đó người được cấp lấy token mới.

**Kết quả:** Nhận 204; API 26 có role vừa thêm. Không tự gỡ các role cũ.

**Trường hợp sai và cách xử lý:**

- Người/role không tồn tại → 404.
- Đã cấp mà Procedure vẫn báo 403 có thể vì JWT cũ chưa chứa role mới; refresh hoặc đăng nhập lại.
- Đừng cấp IT_ADMIN chỉ để né lỗi thiếu role quản lý thủ tục.


Manager không được gán IT_ADMIN, MANAGER hoặc role khác. Chỉ cấp/gỡ FRONT_DESK_OFFICER
cho tài khoản cùng phường chưa có role đặc quyền; vượt phạm vi trả 403. Sau khi gỡ role,
người đó không còn xuất hiện trong danh sách Front Desk của Manager. Admin có thể đổi
phường; Manager không tự chuyển người sang phường mình.

#### API 28 — Gỡ vai trò khỏi tài khoản

`DELETE /api/v1/rbac/users/{userId}/roles/{roleId}`

**Quyền / Header:** RBAC Admin hoặc Manager cấp/gỡ riêng FRONT_DESK_OFFICER cùng phường

**Mục đích:** Thu lại nhóm quyền người đó không còn được giao.

**Cách dùng:** Xem API 26, chọn đúng role cần gỡ, gọi không body.

**Kết quả:** Nhận 204; các role khác của người dùng vẫn giữ nguyên.

**Trường hợp sai và cách xử lý:**

- Người/role không tồn tại → 404; gỡ IT_ADMIN hoạt động cuối cùng → 409.
- Service chỉ đọc JWT có thể còn nhận role trong token cũ đến khi token hết hạn; không coi thay đổi DB là bảo
  đảm thu hồi tức thì ở mọi nơi.


Manager không được gán IT_ADMIN, MANAGER hoặc role khác. Chỉ cấp/gỡ FRONT_DESK_OFFICER
cho tài khoản cùng phường chưa có role đặc quyền; vượt phạm vi trả 403. Sau khi gỡ role,
người đó không còn xuất hiện trong danh sách Front Desk của Manager. Admin có thể đổi
phường; Manager không tự chuyển người sang phường mình.

#### API 29 — Xem nhật ký phân quyền

`GET /api/v1/rbac/audit-logs`

**Quyền / Header:** RBAC Admin

**Mục đích:** Tra cứu ai đã thao tác gì lên role/quyền/tài khoản trong luồng RBAC.

**Cách dùng:** Gọi `?page=1&pageSize=20`, mở từng dòng để xem actor, action, đối tượng và thời gian.

**Kết quả:** Nhận 200 với danh sách và tổng số; details là chuỗi JSON cần parse nếu muốn trình bày thành các
trường.

**Trường hợp sai và cách xử lý:**

- Phân trang không hợp lệ → 400.
- Đây không phải lịch sử mọi hành động trong toàn hệ thống, cũng không có thao tác sửa/xóa log được công bố.


## 2. Procedure Catalog Service (Danh mục & Quản lý thủ tục)

**Chịu trách nhiệm:** Tra cứu thủ tục cho công dân; quản lý nội dung, trạng thái và lịch sử; lưu PDF gốc, bản
nháp đối soát và xuất bản.

### Nhóm 2.1 — Cổng công khai cho Công dân (`/api/v1/procedures`)

Không cần token. Chỉ trả thủ tục đang hoạt động (`isActive=true`); cờ này không thay thế việc xác minh hiệu
lực pháp lý của nội dung.

#### API 30 — Công dân tìm và xem danh sách thủ tục

`GET /api/v1/procedures`

**Quyền / Header:** Public

**Mục đích:** Hiển thị danh sách trên cổng công dân và tìm theo mã/tên/lĩnh vực/cấp thực hiện.

**Cách dùng:** Ví dụ `?keyword=ket%20hon&pageNumber=1&pageSize=10`. Dùng id từ kết quả để mở chi tiết qua API
31.

**Kết quả:** Nhận 200 với danh sách gọn và thông tin phân trang. Chỉ trả thủ tục đang hoạt động; không trả
toàn bộ nội dung chi tiết.

**Trường hợp sai và cách xử lý:**

- pageSize trên 50 hoặc bộ lọc sai → 400.
- Truyền isActive=false cũng không xem được thủ tục đã tắt.
- Không thấy kết quả → thử bỏ bộ lọc/kiểm tra trang; không kết luận thủ tục đã bị xóa.

#### API 31 — Xem chi tiết thủ tục công khai

`GET /api/v1/procedures/{id}`

**Quyền / Header:** Public

**Mục đích:** Dựng trang chi tiết gồm các trường hợp, bước thực hiện, giấy tờ, biểu mẫu và căn cứ pháp lý.

**Cách dùng:** Lấy UUID từ API 30 rồi gọi; đây không phải route tra theo mã thủ tục dạng 1.116789.

**Kết quả:** Nhận 200 với đầy đủ dữ liệu để dựng trang. Mỗi case có thể có các bước và giấy tờ khác nhau.

**Trường hợp sai và cách xử lý:**

- Nhập mã thủ tục thay UUID không đúng route; thủ tục không có/đã tắt → 404.
- Không chỉ hiển thị case đầu tiên rồi bỏ những trường hợp còn lại.
- Link PDF riêng tư cần lấy qua API 32.

#### API 32 — Mở PDF gốc cho công dân

`GET /api/v1/procedures/{id}/source`

**Quyền / Header:** Public

**Mục đích:** Cho người xem đối chiếu trang thủ tục với văn bản PDF gốc đã lưu qua luồng draft.

**Cách dùng:** Gọi khi người dùng bấm Xem PDF, lấy `url` trong response rồi mở URL đó.

**Kết quả:** Nhận 200 với link tạm dùng trong 600 giây. Không phải response bytes PDF trực tiếp.

**Trường hợp sai và cách xử lý:**

- Thủ tục bị tắt hoặc không có draft Published khớp nguồn → 404; chỉ điền OriginalPdfUrl thủ công chưa đủ tạo
  liên kết này.
- Blob chưa cấu hình → 503.
- Link cũ hết hạn → gọi lại API, không lưu cố định link tạm vào DB/FE.


#### API 59 — Danh sách lĩnh vực thủ tục để chọn bộ lọc

`GET /api/v1/procedures/categories`

**Quyền:** Public; Front Desk cũng dùng API này, không cần role Procedure Manager.

**Mục đích:** Cung cấp các lĩnh vực như Hộ tịch, Đất đai để giao diện dựng ô lọc thủ tục.

**Cách dùng:** Gọi không body; lấy id rồi gọi GET procedures?categoryId={id}.
Cán bộ có thể đổi lĩnh vực đang xem; chưa phân công cứng một hay nhiều lĩnh vực cho tài khoản.

**Kết quả:** 200 mảng `{id,categoryName,description?}`, sắp theo tên rồi ID. Trả các danh mục
đã có, kể cả danh mục chưa có thủ tục active; không trả nội dung thủ tục hoặc dữ liệu nhân sự.

**Trường hợp sai:** Không gửi categoryName thay categoryId vào API lọc. Danh mục có thể tồn tại
nhưng danh sách public rỗng nếu không có thủ tục đang hoạt động. Mỗi lần lọc hiện nhận một
categoryId; không truyền danh sách `1,2` vào tham số số nguyên. Front Desk được tự chọn mọi
lĩnh vực, không đồng nghĩa có quyền sửa thủ tục; việc sửa vẫn theo policy ProcedureManager.

### Nhóm 2.2 — Quản lý thủ tục (`/api/v1/procedure-manager/procedures`)

Cần token có **PROCEDURE_MANAGER hoặc IT_ADMIN**. Role MANAGER đơn thuần không đáp ứng policy này. Version là
số nguyên 1, 2, 3…; reason khi đổi trạng thái là tùy chọn.

#### API 33 — Quản trị xem toàn bộ thủ tục

`GET /api/v1/procedure-manager/procedures`

**Quyền / Header:** Bearer: PROCEDURE_MANAGER / IT_ADMIN

**Mục đích:** Quản lý cả thủ tục đang mở và đã tắt, xem số version của mỗi thủ tục.

**Cách dùng:** Ví dụ `?isActive=false&pageNumber=1&pageSize=20` để xem thủ tục đã tắt; bỏ isActive để xem cả
hai.

**Kết quả:** Nhận 200 với trạng thái, số version và phân trang.

**Trường hợp sai và cách xử lý:**

- pageSize trên 100/bộ lọc sai → 400; dùng token công dân hoặc role MANAGER đơn thuần → 403.
- Dùng API public thay API này sẽ thiếu các thủ tục đã tắt.

#### API 34 — Tạo thủ tục mới bằng cách nhập dữ liệu

`POST /api/v1/procedure-manager/procedures`

**Quyền / Header:** Bearer: PROCEDURE_MANAGER / IT_ADMIN

**Mục đích:** Cán bộ tạo thủ tục mới khi đã có dữ liệu đầy đủ, không cần đi qua upload PDF.

**Cách dùng:** Gửi payload mục 4.3, chọn categoryId đã tồn tại và mã thủ tục chưa có.

**Kết quả:** Nhận 201 với thủ tục active và version 1. Dữ liệu có thể xuất hiện ở cổng công dân ngay.

**Trường hợp sai và cách xử lý:**

- Trùng mã → 409; tên quá ngắn/thiếu dữ liệu/danh mục không tồn tại → lỗi validation/nghiệp vụ, sửa theo
  response.
- Không gửi dữ liệu đang đối soát vào API này nếu chưa muốn công khai; dùng draft để chuẩn bị trước.

#### API 35 — Sửa thủ tục và lưu lịch sử

`PUT /api/v1/procedure-manager/procedures/{id}`

**Quyền / Header:** Bearer: PROCEDURE_MANAGER / IT_ADMIN

**Mục đích:** Cập nhật thủ tục đã có, giữ lại trạng thái trước sửa để tra cứu lịch sử.

**Cách dùng:** Điền id; gửi toàn bộ dữ liệu mới theo mục 4.3 cùng decisionNumber/effectiveDate ở cấp ngoài
cùng.

**Kết quả:** Nhận 200 với thủ tục mới; snapshot trạng thái cũ được lưu trong version tiếp theo.

**Trường hợp sai và cách xử lý:**

- ID không có → 404; dữ liệu sai → 400; mã trùng/xung đột → 409.
- Không chỉ gửi một trường như PATCH.
- Ngày hiệu lực không tự hẹn giờ cập nhật; thay đổi được áp dụng khi lệnh thành công.

#### API 36 — Mở hoặc ngừng hiển thị/tiếp nhận thủ tục

`PATCH /api/v1/procedure-manager/procedures/{id}/status`

**Quyền / Header:** Bearer: PROCEDURE_MANAGER / IT_ADMIN

**Mục đích:** Đánh dấu thủ tục đang được sử dụng hay đã đóng mà không xóa dữ liệu.

**Cách dùng:** Gửi `{ "isActive": false, "reason": "Tạm ngừng áp dụng" }`; true để mở lại.

**Kết quả:** Nhận 200 với trạng thái mới. Public không còn trả thủ tục inactive; quản trị vẫn nhìn thấy.

**Trường hợp sai và cách xử lý:**

- Thiếu isActive → 400; id sai → 404.
- API này không tự xử lý/hủy các hồ sơ công dân đã nộp, không nên mô tả cho người dùng rằng toàn bộ hồ sơ liên
  quan đã bị hủy.

#### API 37 — Xem lịch sử phiên bản thủ tục

`GET /api/v1/procedure-manager/procedures/{id}/versions`

**Quyền / Header:** Bearer: PROCEDURE_MANAGER / IT_ADMIN

**Mục đích:** Đối chiếu nội dung trước và sau các lần thay đổi quy định.

**Cách dùng:** Gửi id thủ tục, chọn version từ danh sách và đọc snapshotData.

**Kết quả:** Nhận 200 với các version theo thứ tự giảm dần; snapshotData là object dữ liệu, không phải chuỗi
cần parse thêm.

**Trường hợp sai và cách xử lý:**

- Không có thủ tục → 404.
- Không coi version lớn nhất luôn là nội dung hiện tại: các lần cập nhật lưu snapshot bản trước sửa.
- API chỉ đọc lịch sử, không tự phục hồi một phiên bản.

#### API 38 — Xuất bản dữ liệu đã đối soát theo mã thủ tục

`POST /api/v1/procedure-manager/procedures/publish`

**Quyền / Header:** Bearer: PROCEDURE_MANAGER / IT_ADMIN

**Mục đích:** Nhận dữ liệu đã được cán bộ kiểm tra; tự phân biệt thêm mới hay cập nhật theo procedureCode.

**Cách dùng:** Gửi toàn bộ ReviewedProcedureInput theo mục 4.3. Nếu đang sử dụng draft đã lưu, ưu tiên API 45
để đồng thời hoàn tất draft và liên kết PDF.

**Kết quả:** Nhận 200 ở cả hai trường hợp: mã mới tạo thủ tục/version 1; mã cũ lưu lịch sử rồi cập nhật. Thủ
tục cũ giữ trạng thái hoạt động đang có.

**Trường hợp sai và cách xử lý:**

- Mã gõ nhầm nhưng trùng thủ tục khác có thể cập nhật nhầm thủ tục đó.
- Gửi lặp có thể sinh thêm version, nên tránh bấm nhiều lần; khi timeout kiểm tra kết quả trước khi gửi lại.
- Chưa đủ dữ liệu → 400; xung đột → 409.
- Publish thủ tục đang tắt không tự mở lại.


### Nhóm 2.3 — PDF & Bản nháp đối soát (`/api/v1/procedure-manager/drafts`)

Luồng chính: upload → đọc bản nháp/PDF → chỉnh và lưu → xác nhận xuất bản. Preview chỉ đọc thử, không lưu.
revision là mã chống ghi đè dữ liệu cũ; dùng giá trị mới nhất. AI/OCR tắt vẫn có thể đọc PDF có lớp chữ, nhưng
dữ liệu nghiệp vụ cần được hoàn thiện và đối soát.

#### API 39 — Đọc thử chữ từ PDF

`POST /api/v1/procedure-manager/drafts/extract-preview`

**Quyền / Header:** Bearer: PROCEDURE_MANAGER / IT_ADMIN

**Mục đích:** Kiểm tra PDF có đọc được chữ không, lấy text/gợi ý để hỗ trợ nhập liệu trước khi lưu.

**Cách dùng:** Chọn PDF ở multipart field `file`, gửi Bearer. Không cần Blob nhưng Procedure phải gọi được
AIOCR.

**Kết quả:** Nhận 200 với extractedText, payload và warnings. Không tạo draft, không lưu file, không trả
draftId. AI tắt thì có text nhưng payload cần điền thủ công.

**Trường hợp sai và cách xử lý:**

- File không đúng/ quá lớn → 400 hoặc 413; kết nối chưa cấu hình → 503; không đọc được/dịch vụ phụ thuộc lỗi →
  502; quá thời gian → 504.
- PDF toàn hình có thể không có text khi OCR tắt.
- Nhận 200 không có nghĩa nội dung đã đủ để publish; phải xem warnings.

#### API 40 — Upload PDF và tạo bản nháp có lưu trữ

`POST /api/v1/procedure-manager/drafts`

**Quyền / Header:** Bearer: PROCEDURE_MANAGER / IT_ADMIN

**Mục đích:** Giữ PDF gốc cùng bản nháp để cán bộ đối soát và công dân xem nguồn sau xuất bản.

**Cách dùng:** Gửi multipart `file` là PDF tối đa 20 MiB; cần Blob đã cấu hình. Giữ id trong response để gọi
API 42.

**Kết quả:** Nhận 202 với draft. Nếu trích xuất được bật, theo dõi xử lý tiếp; nếu tắt, chuyển sang đối soát
thủ công. Chưa có thủ tục công khai ở bước này.

**Trường hợp sai và cách xử lý:**

- Upload sai loại/quá lớn → 400/413; Blob chưa cấu hình → 503.
- 202 không có nghĩa AI đã xử lý xong.
- Upload lại cùng file có thể tạo bản nháp khác; kiểm tra draft trước khi thử lại vì lỗi mạng.

#### API 41 — Danh sách bản nháp

`GET /api/v1/procedure-manager/drafts`

**Quyền / Header:** Bearer: PROCEDURE_MANAGER / IT_ADMIN

**Mục đích:** Tìm các PDF đang chờ xử lý, cần đối soát, thất bại hoặc đã xuất bản.

**Cách dùng:** Gọi `?page=1&pageSize=10`, chọn id để mở bản nháp.

**Kết quả:** Nhận 200 với mảng thông tin ngắn gồm trạng thái và failureCode nếu có; chưa có toàn bộ
text/payload.

**Trường hợp sai và cách xử lý:**

- pageSize quá 50/trang sai → 400.
- Response là mảng trực tiếp, không có items/totalCount như danh sách thủ tục; FE đọc nhầm cấu trúc sẽ hiển
  thị sai dù API thành công.

#### API 42 — Mở bản nháp để đối soát

`GET /api/v1/procedure-manager/drafts/{id}`

**Quyền / Header:** Bearer: PROCEDURE_MANAGER / IT_ADMIN

**Mục đích:** Lấy text PDF, các cảnh báo và nội dung đang chỉnh cho màn hình đối soát.

**Cách dùng:** Dùng draftId từ API 40/41. Nếu đang Queued/Processing, kiểm tra lại định kỳ hợp lý; khi
NeedsReview thì cho chỉnh dữ liệu.

**Kết quả:** Nhận 200 với payload và revision mới nhất. Giữ revision để gửi khi lưu/xuất bản.

**Trường hợp sai và cách xử lý:**

- Nhầm procedureId với draftId → có thể 404.
- Failed không phải đã xuất bản; đọc failureCode/warnings.
- Không ghi đè nội dung người dùng đang gõ chỉ vì một lần polling trả về dữ liệu cũ.

#### API 43 — Lưu dữ liệu đã chỉnh của bản nháp

`PUT /api/v1/procedure-manager/drafts/{id}`

**Quyền / Header:** Bearer: PROCEDURE_MANAGER / IT_ADMIN

**Mục đích:** Lưu phần cán bộ đã đọc và sửa trước khi xuất bản.

**Cách dùng:** Gửi `{revision,payload}`; revision lấy từ lần đọc/lưu mới nhất, payload là object thông tin
thủ tục đã chỉnh. Dùng với draft NeedsReview hoặc Failed.

**Kết quả:** Nhận 200 và draft/revision mới; vẫn chưa công khai thủ tục. Lưu revision trả về cho thao tác tiếp
theo.

**Trường hợp sai và cách xử lý:**

- Dùng revision cũ hoặc draft đang xử lý/đã xuất bản → 409; tải lại và đối chiếu, không tự ghi đè.
- Payload quá lớn/sai dạng → 400/413.
- Lưu draft thành công chưa bảo đảm các trường đủ điều kiện xuất bản.

#### API 44 — Thử trích xuất lại bản nháp bị lỗi

`POST /api/v1/procedure-manager/drafts/{id}/retry`

**Quyền / Header:** Bearer: PROCEDURE_MANAGER / IT_ADMIN

**Mục đích:** Chạy lại xử lý sau khi đã sửa nguyên nhân lỗi như cấu hình dịch vụ.

**Cách dùng:** Chỉ dùng draft Failed; gửi `{revision}` mới nhất và cần bật xử lý trích xuất.

**Kết quả:** Nhận 200 với draft về trạng thái xếp hàng; dùng API 42 theo dõi tiếp.

**Trường hợp sai và cách xử lý:**

- Draft không Failed/revision cũ → 409; tính năng trích xuất tắt → 503; id không có → 404.
- Không gọi retry cho draft đang chạy và không nhầm retry với bước sửa thủ công.

#### API 45 — Xác nhận bản nháp và xuất bản chính thức

`POST /api/v1/procedure-manager/drafts/{id}/publish`

**Quyền / Header:** Bearer: PROCEDURE_MANAGER / IT_ADMIN

**Mục đích:** Hoàn tất đối soát, đưa nội dung vào danh mục chính thức cùng PDF gốc và lịch sử phiên bản.

**Cách dùng:** Đọc PDF, sửa và lưu qua API 43 trước; lấy revision mới, gửi
`{ "revision": "<revision thực tế>", "confirmed": true }` khi cán bộ xác nhận.

**Kết quả:** Nhận 200 với chi tiết thủ tục; draft chuyển Published. Việc ghi draft/thủ tục/version nằm trong
transaction.

**Trường hợp sai và cách xử lý:**

- Chưa xác nhận/dữ liệu thiếu → 400; revision cũ, draft chưa sẵn sàng hoặc đã Published → 409; thiếu draft →
  404.
- Không lấy URL tùy ý trong payload để thay nguồn: backend dùng PDF của draft.
- Không gửi lại để tạo version tiếp theo; dùng luồng cập nhật phù hợp.

#### API 46 — Mở PDF gốc của bản nháp

`GET /api/v1/procedure-manager/drafts/{id}/source`

**Quyền / Header:** Bearer: PROCEDURE_MANAGER / IT_ADMIN

**Mục đích:** Hiển thị PDF bên cạnh dữ liệu nhập để cán bộ kiểm tra từng trường.

**Cách dùng:** Gọi với draftId, lấy url trả về để hiển thị PDF/mở tab.

**Kết quả:** Nhận 200 với URL có thời hạn 600 giây; không cần chờ bản nháp được xuất bản.

**Trường hợp sai và cách xử lý:**

- id sai → 404; lưu trữ chưa cấu hình → 503.
- URL hết hạn thì xin URL mới; không đưa link bản nháp cho công dân thay API 32 và không lưu SAS làm liên kết
  vĩnh viễn.


### Nhóm 2.3 — Hoàn thiện quản trị thủ tục theo yêu cầu FE (06/10/2026)

Tám API dưới đây yêu cầu Bearer token có role PROCEDURE_MANAGER hoặc IT_ADMIN theo policy ProcedureManager.
Role MANAGER quản lý phường không tự có quyền quản lý thủ tục. Token sai/hết hạn trả 401; thiếu role trả 403.
Các lỗi nghiệp vụ trả ProblemDetails tiếng Việt với code, traceId và errors khi có lỗi trường dữ liệu.
Đây là API đã triển khai trong source; cần build/deploy bản mới để xuất hiện trên Swagger Azure.

#### API 66 — Quản lý xem chi tiết cả thủ tục đã tắt

**Method & Đường dẫn:** `GET /api/v1/procedure-manager/procedures/{id}`

**Mục đích:** Cán bộ mở form chỉnh sửa thủ tục, kể cả isActive=false, không cần bật công khai trước.

**Cách dùng:** Lấy id từ danh sách quản trị, không body. FE quản trị chuyển sang endpoint này thay vì
GET public `/api/v1/procedures/{id}`.

**Kết quả:** 200 ProcedureDetailDto đầy đủ contentPayload, checklistSchema, formDefinitions, trạng thái,
OriginalPdfUrl/PdfFileName nếu có. API public vẫn 404 với thủ tục đã tắt.

**Lỗi và xử lý:** 404 khi không có thủ tục → tải lại danh sách. Không dùng URL PDF thô để đọc private Blob;
dùng API source thích hợp. API này không thay đổi trạng thái công khai.

#### API 67 — Xóa bản nháp PDF không dùng nữa

**Method & Đường dẫn:** `DELETE /api/v1/procedure-manager/drafts/{id}`

**Mục đích:** Loại bỏ file tải nhầm hoặc bản nháp không sử dụng, đồng thời dọn PDF trên Blob.

**Cách dùng:** Không body. Cho phép NeedsReview, Failed, Queued; trạng thái Deleting được phép thử xóa lại.
FE nên yêu cầu người dùng xác nhận vì thao tác thành công không khôi phục bản nháp được.

**Kết quả:** 204, không body; PDF và bản ghi đã xóa. FE bỏ dòng khỏi danh sách.
Backend ghi nhận Deleting trước khi xóa Blob; trạng thái này ngăn sửa, publish và worker lấy lại bản nháp.

**Lỗi và xử lý:**

- 404: bản nháp không còn tồn tại, kể cả đã xóa trước đó → tải lại danh sách.
- 409 draft.delete_conflict: đang Processing, đã Published hoặc PDF còn được thủ tục/lịch sử tham chiếu.
  Không xóa PDF của thủ tục đã xuất bản; với Processing chờ xử lý kết thúc rồi thử lại nếu cần.
- 503 draft.storage_unavailable: Blob chưa cấu hình hoặc thao tác Blob lỗi → sửa cấu hình/kết nối rồi gọi
  DELETE lại. Nếu đã chuyển Deleting thì bản ghi vẫn tồn tại để retry, không cho sửa/xuất bản.
- Database lỗi sau khi Blob đã xóa: lần gọi DELETE tiếp theo có thể hoàn tất dọn bản ghi; chưa có worker
  tự retry xóa, FE/cán bộ phải gọi lại. Không có transaction phân tán giữa PostgreSQL và Blob.

#### API 68 — Thêm danh mục thủ tục

**Method & Đường dẫn:** `POST /api/v1/procedure-manager/categories`

**Mục đích:** Thêm lĩnh vực để chọn khi tạo/sửa thủ tục hoặc lọc danh sách.

**Cách dùng:** Gửi JSON:

```json
{ "categoryName": "Môi trường", "description": "Thủ tục thuộc lĩnh vực môi trường" }
```

Tên bắt buộc, tối đa 255 ký tự; description tùy chọn, tối đa 4000 ký tự. Backend bỏ khoảng trắng đầu/cuối.

**Kết quả:** 201 ProcedureCategoryDto `{id, categoryName, description}`; description null được bỏ khỏi JSON.
Dùng id mới cho categoryId. Tải lại danh sách bằng GET `/api/v1/procedures/categories`.

**Lỗi và xử lý:** 400 khi thiếu tên/quá dài; 409 category.name_exists khi trùng tên (không phân biệt hoa/thường)
→ chọn danh mục đã có hoặc nhập tên khác. Migration AlignCategoryIdentitySequence cần được áp dụng trước
để ID sinh tự động không trùng các danh mục seed 1–3.

#### API 69 — Sửa tên/mô tả danh mục

**Method & Đường dẫn:** `PUT /api/v1/procedure-manager/categories/{id}`

**Mục đích:** Chỉnh thông tin lĩnh vực; giữ nguyên id và liên kết với các thủ tục hiện có.

**Cách dùng:** Body giống API 68, gửi đầy đủ tên/mô tả mong muốn. Bỏ description hoặc gửi null/rỗng sẽ xóa mô tả.

**Kết quả:** 200 ProcedureCategoryDto. FE cập nhật nhãn danh mục trong dropdown và tải lại danh sách cần thiết.

**Lỗi và xử lý:** 400 dữ liệu sai, 404 category.not_found khi danh mục không còn tồn tại,
409 category.name_exists nếu tên đã thuộc danh mục khác. Sửa tên không tạo version thủ tục.

#### API 70 — Xóa danh mục trống

**Method & Đường dẫn:** `DELETE /api/v1/procedure-manager/categories/{id}`

**Mục đích:** Dọn lĩnh vực không còn dùng.

**Cách dùng:** Không body. Phải chuyển mọi thủ tục đang liên kết sang danh mục khác trước, kể cả thủ tục inactive.

**Kết quả:** 204 không body. FE bỏ danh mục khỏi danh sách.

**Lỗi và xử lý:** 404 nếu không tồn tại; 409 category.in_use kèm thông báo không thể xóa danh mục đang có
thủ tục liên kết. Lịch sử snapshot vẫn giữ categoryId cũ: nếu chỉ còn tham chiếu lịch sử thì được xóa,
nhưng rollback về danh mục đã xóa sẽ bị chặn 409 và cần đối soát/cập nhật thủ công.

#### API 71 — Khôi phục nội dung từ phiên bản lịch sử

**Method & Đường dẫn:** `POST /api/v1/procedure-manager/procedures/{id}/versions/{versionNumber}/rollback`

**Mục đích:** Khôi phục nội dung sai mà không phải nhập lại từng trường và không mất lịch sử.

**Cách dùng:** Lấy versionNumber từ API lịch sử (không dùng GUID versionId ở route này), gửi:

```json
{
  "reason": "Khôi phục nội dung trước khi chỉnh nhầm",
  "decisionNumber": "QD-2026-01",
  "effectiveDate": "2026-10-06"
}
```

Reason bắt buộc, tối đa 4000 ký tự; decisionNumber bắt buộc, tối đa 100 ký tự; effectiveDate là ngày hợp lệ.
Backend khóa thủ tục, chụp trạng thái trước rollback vào version kế tiếp rồi khôi phục dữ liệu snapshot
đã chọn trong cùng transaction. Giữ nguyên id, createdAt, isActive và lý do bật/tắt hiện tại; cập nhật updatedAt.
Số quyết định trong contentPayload được thay bằng decisionNumber của lần khôi phục này.

**Kết quả:** 200 ProcedureDetailDto. Lịch sử mới giữ snapshot TRƯỚC rollback theo quy ước version hiện có;
`snapshotData.rollback` ghi restoredFromVersion, reason, changedBy, decisionNumber, effectiveDate.
Không xóa/sửa các version cũ. FE tải lại chi tiết và lịch sử sau thành công.

**Lỗi và xử lý:**

- 400 khi body/số phiên bản sai; 404 khi thủ tục hoặc phiên bản của thủ tục không tồn tại.
- 409 procedure.invalid_snapshot khi snapshot thiếu/sai dữ liệu → đối soát và sửa thủ công.
- 409 procedure.category_not_found khi danh mục lịch sử đã bị xóa; procedure.code_exists khi mã cũ đã được
  thủ tục khác dùng → cập nhật thủ công với danh mục/mã phù hợp.
- Thủ tục đang tắt vẫn tắt sau rollback. Gọi lại là một hành động rollback mới, có version mới;
  FE không tự retry khi chưa biết lần trước thành công hay chưa.

#### API 72 — Danh sách biểu mẫu để chọn cho thủ tục

**Method & Đường dẫn:** `GET /api/v1/procedure-manager/document-forms?page=1&pageSize=50&searchCode=CT`

**Mục đích:** Cán bộ chọn formTemplateId từ dropdown, không nhập GUID thủ công.

**Cách dùng:** Không body. Page từ 1 đến 1000000, pageSize 1–100; searchCode tùy chọn, tối đa 100 ký tự.
Procedure gọi DocumentForm qua HTTP và chỉ lấy biểu mẫu active. Cấu hình DocumentForm__BaseUrl phải trỏ
đúng service (Compose dùng http://document-form:8080/, local mặc định http://localhost:5004/).

**Kết quả:** 200 mảng `{id, formCode, formName, formType}` của trang yêu cầu; dùng id làm formTemplateId.
API này không trả metadata phân trang; khi cần thêm, FE gọi trang kế tiếp cho đến mảng rỗng.
Hợp đồng DocumentForm hiện cung cấp mẫu DOCX, nên formType là DOCX_TEMPLATE; chưa có danh sách mẫu
ONLINE_INTERACTIVE riêng. Mẫu chưa có file Word vẫn có thể xuất hiện; cần kiểm tra mẫu trước khi sử dụng.

**Lỗi và xử lý:** 400 query sai; 502 document_form.invalid_response khi dữ liệu nguồn sai;
503 khi thiếu địa chỉ hoặc DocumentForm không sẵn sàng; 504 timeout. Không hiển thị mảng rỗng giả khi service lỗi.
Không truy vấn chéo database, không đưa secret liên dịch vụ vào FE.

#### API 73 — Đọc PDF gốc của một phiên bản lịch sử

**Method & Đường dẫn:** `GET /api/v1/procedure-manager/procedures/{id}/versions/{versionId}/source`

**Mục đích:** Mở PDF của phiên bản cũ để đối chiếu, kể cả thủ tục hiện đã tắt hoặc đã thay PDF mới.

**Cách dùng:** Lấy GUID id của version từ API lịch sử (không dùng versionNumber), không body.

**Kết quả:** 200 `{url, expiresInSeconds: 600}`, Cache-Control no-store. URL chỉ có quyền đọc và hết hạn
sau 10 phút; FE xin link mới khi hết hạn, không lưu SAS vào dữ liệu thủ tục.

**Lỗi và xử lý:** 404 khi version không thuộc thủ tục, không có PDF đã upload qua draft, hoặc Blob đã mất;
503 khi cấu hình/lưu trữ chưa sẵn sàng. Hệ thống chỉ ký URL của Blob ghi nhận trong draft Published của
thủ tục đó, không tự ký URL ngoài hoặc URL tùy ý trong snapshot.

## 3. Document & Form Service — chỉnh sửa DOCX trên web

Luồng hiện tại (07/10/2026): admin upload mẫu DOCX nguyên bản; FE tải binary vào editor Word trên web;
người dân sửa trong editor; FE export DOCX và gửi lại khi lưu nháp. Backend lưu nguyên byte, không sinh
trang khai báo bổ sung, không yêu cầu schema, mapping hay placeholder. File người dân nằm ở blob riêng.
Mẫu gốc không bị ghi đè khi người dân lưu. Việc hiển thị và bảo toàn bố cục khi chỉnh sửa phụ thuộc editor FE.

DocumentForm quản lý **một đơn**, không quản lý toàn bộ thủ tục/giấy tờ. Procedure giữ templateId trong
thành phần hồ sơ; Workflow có thể tham chiếu submissionId khi hoàn thành đơn. Không truy vấn chéo database.

### 3.1. Nhóm mẫu DOCX (admin quản lý, người dân đọc)

| API | Ai gọi, lúc nào và request | Thành công | Lỗi thường gặp và cách xử lý |
|---|---|---|---|
| `GET /api/v1/form-templates` | Admin lấy danh sách; query page=1,pageSize=20,isActive=true,searchCode tùy chọn. | 200 danh sách phân trang | 400 query không hợp lệ: sửa phân trang. |
| `POST /api/v1/form-templates` | Admin tạo mẫu; JSON `{ "code":"DON_01", "title":"Đơn mẫu" }`. | 201, lưu `id` làm templateId | 400 thiếu dữ liệu; 409 trùng code: dùng mẫu đã có hoặc code khác. |
| `GET /api/v1/form-templates/{templateId}` | FE gọi khi người dân chọn “Điền đơn”. | 200 metadata, `onlineReady=true` khi active và có file; không còn schemaDefinition/templateVersionId | 404: kiểm tra ID; onlineReady=false: admin upload DOCX hoặc kiểm tra trạng thái. |
| `POST /api/v1/form-templates/{templateId}/upload-docx` | Admin thêm/thay mẫu chính thức; multipart field `file`, tối đa 20 MiB. | 200 `{templateId,blobUrl}`; blob mới, bytes nguyên bản | 400 rỗng/hỏng/sai đuôi/quá lớn: chọn DOCX hợp lệ; 404 sai templateId. |
| `GET /api/v1/form-templates/{templateId}/download-docx` | FE fetch binary rồi import vào editor khi bắt đầu đơn mới; admin đối chiếu mẫu. | 200 binary DOCX nguyên bản | 404 thiếu mẫu/file: kiểm tra ID hoặc upload lại. |

### 3.2. Nhóm đơn của người dân

| API | Ai gọi, lúc nào và request | Thành công | Lỗi thường gặp và cách xử lý |
|---|---|---|---|
| `POST /api/v1/citizen/submissions/draft` | FE lần đầu lưu: multipart `templateId`, `applicantId`, `file` (DOCX export từ editor). | 201 `{submissionId,status:"Draft",fileName,fileSizeBytes,updatedAt,message}` | 400 file/ID không hợp lệ; 404 mẫu chưa upload/không có; 409 mẫu inactive; 415 gửi JSON: đổi multipart; 503 Blob lỗi: thử lại. |
| `PUT /api/v1/citizen/submissions/{submissionId}/draft` | FE lưu tiếp: multipart `applicantId`, `file`. Không đổi templateId. | 200 cùng DTO lưu nháp; file mới được lưu nguyên byte | 400 đơn đã chốt/file lỗi; 403 sai chủ; 404 thiếu đơn/mẫu; 409 xung đột: tải lại trước khi lưu; 503 Blob lỗi. |
| `GET /api/v1/citizen/submissions?applicantId=...` | FE màn hình “Đơn của tôi”; thêm status,page,pageSize nếu cần. | 200 phân trang metadata | 400 thiếu applicantId hoặc page/pageSize/status sai: sửa query. |
| `GET /api/v1/citizen/submissions/{submissionId}?applicantId=...` | FE mở lại đơn, đọc status trước khi mở editor. | 200 metadata (id,templateId,applicantId,fileName,fileSizeBytes,status,…); không formData/schema | 403 sai chủ; 404 không tồn tại: kiểm tra ID. |
| `GET /api/v1/citizen/submissions/{submissionId}/download-docx?applicantId=...` | FE mở **bản đã sửa**, kể cả sau khi admin đổi mẫu; có thể dùng để tải/in. | 200 binary đúng file đã lưu gần nhất | 403 sai chủ; 404 thiếu đơn/blob: kiểm tra ID hoặc khôi phục file. |
| `POST /api/v1/citizen/submissions/{submissionId}/submit` | Sau khi lưu thành công và người dân xác nhận hoàn thành; JSON `{ "applicantId":"UUID" }`. | 200 status=Submitted, khóa sửa nháp | 400 đã chốt; 403 sai chủ; 404 thiếu đơn/file; 409 xung đột: mở lại kiểm tra. |

File phải là DOCX hợp lệ không macro, tối đa 20 MiB; ZIP tối đa 5.000 entries/100 MiB giải nén.
Backend không thể suy ra tất cả chỗ trống đã điền từ DOCX tùy ý. `submit` xác nhận của người dân,
không phải kiểm tra required fields hay nộp toàn bộ hồ sơ hành chính. FE cần lưu thành công trước khi submit.

Đã bỏ API `PUT .../online-config`; luồng JSON formData cũ không còn được nhận. Các cột lịch sử
schema/version trong database được giữ để không mất dữ liệu cũ; không cần migration cho thay đổi này.

Lỗi nhóm Citizen dùng ProblemDetails (`status,title,detail,code,traceId`); model binding dùng
ValidationProblemDetails. Nhóm template giữ `{code,message}` cho lỗi nghiệp vụ. FE đọc HTTP status trước.
Hiện applicantId vẫn theo hợp đồng tích hợp sẵn có; controller chưa tự xác thực JWT/Authorize.
Danh tính này cần đến từ lớp xác thực tin cậy của hệ thống, không coi việc có UUID là xác thực.

## 4. AIOCR Service (Trích xuất dữ liệu PDF)

**Chịu trách nhiệm:** Đọc chữ PDF và thực hiện các bước AI/OCR khi được cấu hình. Service Procedure gọi nội
bộ; FE dùng API preview/draft của Procedure.

### Nhóm 4.1 — Trích xuất PDF nội bộ

Request là bytes PDF trực tiếp, không phải multipart. Service key chỉ dùng phía backend.

#### API 53 — Service Procedure nhờ AIOCR trích xuất PDF

`POST /internal/v1/procedure-extractions`

**Quyền / Header:** `X-Service-Key`

**Mục đích:** Kết nối hai service: Procedure gửi file, AIOCR đọc chữ và có thể gọi AI/OCR tùy cấu hình. Người
dùng FE không gọi API này trực tiếp.

**Cách dùng:** Backend gửi bytes PDF với Content-Type application/pdf và X-Service-Key đã cấu hình. Không dùng
multipart và không thay service key bằng access token người dùng.

**Kết quả:** Nhận 200 với text, payload, warnings. Đọc chữ PDF, OCR ảnh và AI hiểu nội dung là các bước riêng;
AI/OCR tắt vẫn có thể đọc được PDF có lớp chữ.

**Trường hợp sai và cách xử lý:**

- Key sai → 401; tính năng/key chưa cấu hình → 503; PDF không hợp lệ → 400; quá giới hạn → 413; xử lý phụ
  thuộc lỗi/timeout → 502/504.
- Không đưa service key vào mã FE.
- Payload trống nhưng có text khi AI tắt không phải bằng chứng trích xuất text thất bại.


## 5. YARP Gateway & Endpoints hệ thống chung

**Chịu trách nhiệm:** Gateway tiếp nhận và chuyển tiếp request đến service phù hợp; các service tự thực thi
quyền nghiệp vụ. Health/root phục vụ nhận biết tiến trình, không phải API nghiệp vụ.

### Nhóm 5.1 — Kiểm tra service và Swagger

- **Method & Đường dẫn:** `GET /health`
  - **Service:** Gateway và cả 6 service
  - **Mục đích & Cách sử dụng:** Mở tại đúng base URL để kiểm tra ứng dụng phản hồi; thông thường 200 với
    `Healthy`.
  - **Lưu ý khi kết quả không như mong đợi:** Chưa kiểm tra đầy đủ PostgreSQL/Blob/AI; không dùng Healthy để
    kết luận mọi API sẵn sàng. Không kết nối được thì kiểm tra tiến trình/port.

- **Method & Đường dẫn:** `GET /`
  - **Service:** 6 service, không gồm Gateway
  - **Mục đích & Cách sử dụng:** Trả tên microservice; DocumentForm có thêm version/status.
  - **Lưu ý khi kết quả không như mong đợi:** 200 không chứng minh database hoạt động. Root Gateway không có
    endpoint này.

- **Method & Đường dẫn:** `GET /swagger/index.html`
  - **Service:** IAM, Procedure, DocumentForm khi Development
  - **Mục đích & Cách sử dụng:** Giao diện thử API: Try it out → điền dữ liệu → Execute; Authorize nếu cần
    token.
  - **Lưu ý khi kết quả không như mong đợi:** 404 có thể vì sai service hoặc không bật Development, không nhất
    thiết lỗi đăng nhập.

- **Method & Đường dẫn:** `GET /swagger/v1/swagger.json`
  - **Service:** IAM, Procedure, DocumentForm khi Development
  - **Mục đích & Cách sử dụng:** Bản mô tả OpenAPI để công cụ đọc hợp đồng API.
  - **Lưu ý khi kết quả không như mong đợi:** Không phải dữ liệu nghiệp vụ; các service còn lại chưa có
    Swagger tương ứng.


ApplicationWorkflow đã có API hồ sơ (xem mục TASK-11 bên dưới); AnalyticsSystem mới có root/health. Tổng cộng có 6 root và 7
health endpoint theo host.

### Nhóm 5.2 — Các tuyến chuyển tiếp của Gateway (local: cổng 5000)

`ANY` nghĩa Gateway chuyển tiếp method của request; không có nghĩa service đích chấp nhận mọi method. `{**}`
thể hiện phần đường dẫn còn lại.

- **Method & Đường dẫn:** `ANY /api/v1/auth/{**}`
  - **Service đích:** IAM
  - **Mục đích & Cách sử dụng:** Đăng ký/đăng nhập/refresh/logout, giữ nguyên route. Dùng tuyến này để khớp
    Path cookie refresh.

- **Method & Đường dẫn:** `ANY /api/v1/users/{**}`
  - **Service đích:** IAM
  - **Mục đích & Cách sử dụng:** Thông tin người dùng và hồ sơ, giữ nguyên route.

- **Method & Đường dẫn:** `ANY /api/v1/accounts/{**}`
  - **Service đích:** IAM
  - **Mục đích & Cách sử dụng:** Quản trị tài khoản, giữ nguyên route.

- **Method & Đường dẫn:** `ANY /api/v1/rbac/{**}`
  - **Service đích:** IAM
  - **Mục đích & Cách sử dụng:** Quản lý vai trò/quyền, giữ nguyên route.

- **Method & Đường dẫn:** `ANY /api/v1/procedures/{**}`
  - **Service đích:** Procedure
  - **Mục đích & Cách sử dụng:** Danh mục công dân, chi tiết và nguồn PDF, giữ nguyên route.

- **Method & Đường dẫn:** `ANY /api/v1/procedure-manager/{**}`
  - **Service đích:** Procedure
  - **Mục đích & Cách sử dụng:** Quản trị thủ tục và draft, giữ nguyên route.

- **Method & Đường dẫn:** `ANY /api/document-form/{**}`
  - **Service đích:** DocumentForm
  - **Mục đích & Cách sử dụng:** Bỏ prefix `/api/document-form`. Ví dụ
    `/api/document-form/api/v1/form-templates`. Không có tuyến trực tiếp `/api/v1/form-templates` trong cấu
    hình hiện tại.

- **Method & Đường dẫn:** `ANY /api/ai-ocr/{**}`
  - **Service đích:** AIOCR
  - **Mục đích & Cách sử dụng:** Bỏ prefix `/api/ai-ocr`; API nội bộ vẫn yêu cầu service key. Không đưa key
    vào FE.

- **Method & Đường dẫn:** `ANY /api/application-workflow/{**}`
  - **Service đích:** ApplicationWorkflow
  - **Mục đích & Cách sử dụng:** Bỏ prefix tương ứng, ví dụ `/api/application-workflow/health`.

- **Method & Đường dẫn:** `ANY /api/analytics-system/{**}`
  - **Service đích:** Analytics
  - **Mục đích & Cách sử dụng:** Bỏ prefix tương ứng, ví dụ `/api/analytics-system/health`.

- **Method & Đường dẫn:** `ANY /api/iam/{**}`
  - **Service đích:** IAM
  - **Mục đích & Cách sử dụng:** Tuyến prefix cũ. Ưu tiên `/api/v1/auth/...` cho auth để trình duyệt gửi đúng
    cookie.

- **Method & Đường dẫn:** `ANY /api/procedure-catalog/{**}`
  - **Service đích:** Procedure
  - **Mục đích & Cách sử dụng:** Tuyến prefix thay thế, bỏ `/api/procedure-catalog` trước khi chuyển tiếp.


## Quy tắc cập nhật

Mỗi khi thêm, đổi hoặc xóa API, cập nhật **chính file này** trong cùng task. Xếp API vào đúng service/nhóm;
ghi quyền thật, mục đích, cách gọi, kết quả và trường hợp sai/cách khắc phục. Cập nhật DTO/ví dụ trong phụ lục
khi hợp đồng đổi. Không ghi tính năng hoặc quyền dự kiến thành chức năng đã có. Quy tắc duy trì tài liệu đã có
trong AGENTS.md.

---

# Phụ lục kỹ thuật — Cổng, DTO, JSON mẫu và cấu hình

Các mục 1–9 dưới đây là phần tra cứu chi tiết bổ trợ cho các nhóm API ở trên.

## 1. Địa chỉ và quy ước gọi API

### 1.1. Local Dev

Các cổng dưới đây lấy từ `Properties/launchSettings.json`, áp dụng khi chạy bằng launch profile tương ứng.
Docker/Azure dùng địa chỉ và port đã cấu hình cho môi trường đó.

- **Thành phần:** YARP Gateway
  - **Base URL trực tiếp:** `http://localhost:5000`
  - **Swagger UI khi bật môi trường Development:** Không có Swagger tổng hợp

- **Thành phần:** IAM
  - **Base URL trực tiếp:** `http://localhost:5001`
  - **Swagger UI khi bật môi trường Development:** `http://localhost:5001/swagger`

- **Thành phần:** Procedure Catalog
  - **Base URL trực tiếp:** `http://localhost:5002`
  - **Swagger UI khi bật môi trường Development:** `http://localhost:5002/swagger`

- **Thành phần:** Application Workflow
  - **Base URL trực tiếp:** `http://localhost:5003`
  - **Swagger UI khi bật môi trường Development:** `http://localhost:5003/swagger`

- **Thành phần:** Document Form
  - **Base URL trực tiếp:** `http://localhost:5004`
  - **Swagger UI khi bật môi trường Development:** `http://localhost:5004/swagger`

- **Thành phần:** AIOCR
  - **Base URL trực tiếp:** `http://localhost:5005`
  - **Swagger UI khi bật môi trường Development:** Chưa cấu hình

- **Thành phần:** Analytics System
  - **Base URL trực tiếp:** `http://localhost:5006`
  - **Swagger UI khi bật môi trường Development:** Chưa cấu hình


### 1.2. Gọi qua Gateway

- **Service:** IAM
  - **Đường dẫn qua Gateway:** Giữ nguyên `/api/v1/auth`, `/api/v1/users`, `/api/v1/accounts`, `/api/v1/rbac`
  - **Ví dụ:** `http://localhost:5000/api/v1/auth/login`

- **Service:** Procedure
  - **Đường dẫn qua Gateway:** Giữ nguyên `/api/v1/procedures` và `/api/v1/procedure-manager`
  - **Ví dụ:** `http://localhost:5000/api/v1/procedures?pageNumber=1`

- **Service:** Document Form
  - **Đường dẫn qua Gateway:** Thêm `/api/document-form` trước route service
  - **Ví dụ:** `http://localhost:5000/api/document-form/api/v1/form-templates`

- **Service:** AIOCR
  - **Đường dẫn qua Gateway:** Thêm `/api/ai-ocr` trước route service
  - **Ví dụ:** `/api/ai-ocr/internal/v1/procedure-extractions` — dành cho backend

- **Service:** Application Workflow
  - **Đường dẫn qua Gateway:** Thêm `/api/application-workflow`
  - **Ví dụ:** `/api/application-workflow/health`

- **Service:** Analytics
  - **Đường dẫn qua Gateway:** Thêm `/api/analytics-system`
  - **Ví dụ:** `/api/analytics-system/health`


Gateway cũng có prefix `/api/iam` và `/api/procedure-catalog`, được loại bỏ trước khi chuyển tiếp. **Với IAM,
dùng route chuẩn `/api/v1/auth/...` **, vì cookie refresh có Path `/api/v1/auth`; dùng prefix cũ có thể khiến
trình duyệt không gửi cookie đến route refresh. Gateway hiện không có route trực tiếp `/api/v1/form-templates`
.

### 1.3. Quy ước

- Route trong các bảng bên dưới là route **tại service**, chưa có prefix Gateway.
- `{id}`, `{userId}`, `{templateId}` là UUID; `{roleId}`, `{permissionId}` là số nguyên. Thay cả dấu ngoặc
  bằng giá trị thật.
- JSON request dùng `Content-Type: application/json`. Upload dùng `multipart/form-data`, trường `file`; để
  trình duyệt/Postman tự tạo boundary.
- Endpoint yêu cầu đăng nhập: `Authorization: Bearer <accessToken>`. Swagger: đăng nhập trước, lấy
  `accessToken`, nhấn **Authorize** và nhập theo mô tả security scheme.
- Ngày thuần túy có dạng `YYYY-MM-DD`; thời điểm có dạng ISO 8601, ví dụ `2026-10-04T10:00:00Z`.
- `?` sau tên trường DTO trong tài liệu nghĩa là nullable/tùy chọn; không phải một phần tên JSON. IAM bỏ
  trường null khỏi response. FE không nên mặc định các service khác cũng làm như vậy.
- `204` không có JSON body. Các mã lỗi trong bảng là lỗi điển hình; endpoint được bảo vệ còn có `401/403`, và
  có thể gặp lỗi hạ tầng `500`.

## 2. IAM: đăng ký, đăng nhập và token

### 2.1. Danh mục xác thực

**Cả 4 API POST dưới đây đều cần header `X-CSRF-Protection: 1`.** “Public” chỉ có nghĩa không cần Bearer,
không miễn kiểm tra CSRF.

- **Method:** POST
  - **Route:** `/api/v1/auth/register`
  - **Quyền / Request:** Public; `{username,email,password,fullName}`
  - **Thành công:** `201 CurrentUserDto`, Location `/api/v1/users/me`
  - **Lỗi điển hình:** `400` dữ liệu sai; `409` trùng tài khoản

- **Method:** POST
  - **Route:** `/api/v1/auth/login`
  - **Quyền / Request:** Public; `{usernameOrEmail,password}`
  - **Thành công:** `200 BrowserAuthResponse` và Set-Cookie refresh
  - **Lỗi điển hình:** `401` thông tin sai/tài khoản không hợp lệ

- **Method:** POST
  - **Route:** `/api/v1/auth/refresh-token`
  - **Quyền / Request:** Public; **không body**, gửi cookie refresh
  - **Thành công:** `200 BrowserAuthResponse` và cookie mới
  - **Lỗi điển hình:** `401` thiếu/hết hạn/đã thu hồi token

- **Method:** POST
  - **Route:** `/api/v1/auth/revoke-token`
  - **Quyền / Request:** Bearer; **không body**, gửi cookie refresh
  - **Thành công:** `204`, thu hồi refresh và xóa cookie
  - **Lỗi điển hình:** `401/403`

- **Method:** GET
  - **Route:** `/api/v1/users/me`
  - **Quyền / Request:** Bearer + `iam.profile.read`; không body
  - **Thành công:** `200 CurrentUserDto`
  - **Lỗi điển hình:** `401/403`


`BrowserAuthResponse` gồm `accessToken`, `accessTokenExpiresAt`, `refreshTokenExpiresAt`, `tokenType`
(`Bearer`). **Không có refreshToken trong JSON**. Đăng ký tạo sẵn hồ sơ và gán `REGISTERED_CITIZEN`; không tự
đăng nhập.

`CurrentUserDto` gồm `id`, `username`, `email`, `profile?` (UserProfileDto bên dưới), `roles` (mảng tên
role), `permissions` (mảng mã quyền).

Validation đăng ký:

- `username`: bắt buộc, tối đa 100, chỉ chữ ASCII, số, `_`, `.`, `-`.
- `email`: bắt buộc, email hợp lệ, tối đa 255.
- `password`: ít nhất 8 ký tự, có chữ hoa và ký tự dấu câu/ký hiệu; tối đa **72 byte UTF-8**. Không có yêu
  cầu riêng bắt buộc chữ số trong validator hiện tại.
- `fullName`: bắt buộc, tối đa 255.

### 2.2. HttpOnly, CSRF và CORS

Cookie `refreshToken` có `HttpOnly=true`, Path `/api/v1/auth`, không đặt Domain. JavaScript không đọc cookie
này. Mặc định SameSite Strict; Secure được áp dụng trừ cấu hình cho phép HTTP localhost trong Development. Khi
FE/API khác site trên môi trường thật, cần cấu hình cookie/CORS tương ứng; không chỉ đổi URL trong FE là đủ.

Nếu request có `Origin`, IAM chấp nhận khi Origin trùng scheme/host hiện tại hoặc nằm trong AllowedOrigins.
Header CSRF vẫn bắt buộc kể cả không có Origin. Forwarded Headers giúp nhận đúng HTTPS/host bên ngoài reverse
proxy. Gặp `403 iam.csrf_rejected`: kiểm tra custom header, Origin, CORS và forwarded host/proto.

Ví dụ FE (biến đăng nhập do người dùng nhập, không hardcode tài khoản):

```javascript
const apiBase = 'http://localhost:5000';
const response = await fetch(`${apiBase}/api/v1/auth/login`, {
  method: 'POST',
  credentials: 'include',
  headers: { 'Content-Type': 'application/json', 'X-CSRF-Protection': '1' },
  body: JSON.stringify({ usernameOrEmail, password })
});
if (!response.ok) throw await response.json();
const auth = await response.json();
const me = await fetch(`${apiBase}/api/v1/users/me`, {
  headers: { Authorization: `Bearer ${auth.accessToken}` }
});

// Khi cần refresh: không truyền accessToken/refreshToken vào JSON.
const renewed = await fetch(`${apiBase}/api/v1/auth/refresh-token`, {
  method: 'POST', credentials: 'include',
  headers: { 'X-CSRF-Protection': '1' }
});
```

FE dùng `credentials: 'include'` cho login/refresh/logout, giữ cùng hostname và base URL trong suốt phiên.
Refresh token xoay vòng một lần sử dụng: gom các request refresh đồng thời thành một lần gọi. Khi refresh thất
bại 401, quay về đăng nhập. Logout gửi Bearer và cookie, sau đó xóa access token phía FE; endpoint thu hồi
refresh hiện tại, không phải thao tác đăng xuất mọi thiết bị.

Ngoại lệ RBAC: PUT/DELETE user-role cho phép MANAGER cấp/gỡ riêng FRONT_DESK_OFFICER
cùng phường. Các thao tác tạo role, sửa quyền và xem audit vẫn chỉ dành cho Admin.
CurrentUserDto bổ sung wardId nullable; token/cookie không đổi. Phạm vi quản lý luôn đọc từ DB.

## 3. IAM: hồ sơ, tài khoản và RBAC

### 3.1. Hồ sơ cá nhân và quản trị hồ sơ

`ProfileInput` và `UserProfileDto` có cùng các trường:

```json
{
  "fullName": "Người dùng kiểm thử",
  "identityNumber": null,
  "phoneNumber": null,
  "dateOfBirth": "1995-01-01",
  "gender": null,
  "permanentAddress": null,
  "temporaryAddress": null
}
```

Tên bắt buộc/tối đa 255; số định danh tối đa 20 và duy nhất nếu có; điện thoại tối đa 20; giới tính tối đa 10;
địa chỉ tối đa 4000; ngày sinh không được ở tương lai. `PUT` thay toàn bộ hồ sơ: trường tùy chọn bỏ
qua/null/trắng được xóa về null.

- **Method:** GET
  - **Route:** `/api/v1/users/me/profile`
  - **Quyền / Request:** `iam.profile.read`; không body
  - **Thành công:** `200 UserProfileDto`
  - **Lỗi nghiệp vụ:** `404` chưa có hồ sơ

- **Method:** POST
  - **Route:** `/api/v1/users/me/profile`
  - **Quyền / Request:** `iam.profile.write`; ProfileInput
  - **Thành công:** `201 UserProfileDto`
  - **Lỗi nghiệp vụ:** `400/409`

- **Method:** PUT
  - **Route:** `/api/v1/users/me/profile`
  - **Quyền / Request:** `iam.profile.write`; ProfileInput
  - **Thành công:** `200 UserProfileDto`
  - **Lỗi nghiệp vụ:** `400/404/409`

- **Method:** DELETE
  - **Route:** `/api/v1/users/me/profile`
  - **Quyền / Request:** `iam.profile.write`; không body
  - **Thành công:** `204`
  - **Lỗi nghiệp vụ:** `404/409`

- **Method:** GET
  - **Route:** `/api/v1/users/{userId}/profile`
  - **Quyền / Request:** AccountManagement, đúng phạm vi; không body
  - **Thành công:** `200 UserProfileDto`
  - **Lỗi nghiệp vụ:** `404`

- **Method:** POST
  - **Route:** `/api/v1/users/{userId}/profile`
  - **Quyền / Request:** AccountManagement, đúng phạm vi; ProfileInput
  - **Thành công:** `201 UserProfileDto`
  - **Lỗi nghiệp vụ:** `400/404/409`

- **Method:** PUT
  - **Route:** `/api/v1/users/{userId}/profile`
  - **Quyền / Request:** AccountManagement, đúng phạm vi; ProfileInput
  - **Thành công:** `200 UserProfileDto`
  - **Lỗi nghiệp vụ:** `400/404/409`

- **Method:** DELETE
  - **Route:** `/api/v1/users/{userId}/profile`
  - **Quyền / Request:** AccountManagement, đúng phạm vi; không body
  - **Thành công:** `204`
  - **Lỗi nghiệp vụ:** `404/409`


Đăng ký đã tạo hồ sơ nên POST hồ sơ ngay sau đăng ký sẽ xung đột; dùng PUT để sửa. Xóa hồ sơ không xóa tài
khoản.

### 3.2. Quản lý tài khoản

Cả 3 endpoint cần AccountManagement: IT_ADMIN + iam.manage quản lý toàn hệ thống; MANAGER chỉ quản lý Front Desk cùng phường.

- **Method:** GET
  - **Route:** `/api/v1/accounts`
  - **Request / Công dụng:** Query `page=1&pageSize=20`; danh sách tài khoản
  - **Thành công:** `200 AccountPage`
  - **Lỗi nghiệp vụ:** `400`

- **Method:** GET
  - **Route:** `/api/v1/accounts/{userId}`
  - **Request / Công dụng:** Không body; xem một tài khoản
  - **Thành công:** `200 AccountDto`
  - **Lỗi nghiệp vụ:** `404`

- **Method:** PUT
  - **Route:** `/api/v1/accounts/{userId}/status`
  - **Request / Công dụng:** `{ "isActive": false }` khóa; true mở lại
  - **Thành công:** `204`
  - **Lỗi nghiệp vụ:** `400/404/409`


`AccountDto`: `id, username, email, isActive, createdAt, wardId?`.
`AccountPage`: `{items: AccountDto[], page, pageSize, total}`; pageSize 1–100.

Không được tự khóa tài khoản đang dùng hoặc khóa IT_ADMIN hoạt động cuối cùng. Khóa tài khoản thu hồi refresh
token; mở lại không khôi phục các refresh token đã thu hồi. Không có ngày hết hạn khóa trong request này.

### 3.3. Quản lý RBAC

Các API RBAC cần tài khoản hiện tại còn hoạt động, có role **IT_ADMIN** và quyền **iam.manage** theo dữ liệu
IAM.

- **Method:** GET
  - **Route:** `/api/v1/rbac/roles`
  - **Request / Công dụng:** Query `page=1&pageSize=20`
  - **Thành công:** `200 RbacPage<RoleDto>`
  - **Lỗi nghiệp vụ:** `400`

- **Method:** GET
  - **Route:** `/api/v1/rbac/roles/{roleId}`
  - **Request / Công dụng:** Xem role
  - **Thành công:** `200 RoleDto`
  - **Lỗi nghiệp vụ:** `404`

- **Method:** POST
  - **Route:** `/api/v1/rbac/roles`
  - **Request / Công dụng:** RoleInput; tạo role tùy chỉnh
  - **Thành công:** `201 RoleDto`
  - **Lỗi nghiệp vụ:** `400/409`

- **Method:** PUT
  - **Route:** `/api/v1/rbac/roles/{roleId}`
  - **Request / Công dụng:** RoleInput; sửa role
  - **Thành công:** `200 RoleDto`
  - **Lỗi nghiệp vụ:** `400/404/409`

- **Method:** DELETE
  - **Route:** `/api/v1/rbac/roles/{roleId}`
  - **Request / Công dụng:** Xóa role tùy chỉnh
  - **Thành công:** `204`
  - **Lỗi nghiệp vụ:** `404/409`

- **Method:** GET
  - **Route:** `/api/v1/rbac/permissions`
  - **Request / Công dụng:** Danh sách quyền có sẵn
  - **Thành công:** `200 PermissionDto[]`
  - **Lỗi nghiệp vụ:** —

- **Method:** GET
  - **Route:** `/api/v1/rbac/roles/{roleId}/permissions`
  - **Request / Công dụng:** Quyền của role
  - **Thành công:** `200 PermissionDto[]`
  - **Lỗi nghiệp vụ:** `404`

- **Method:** PUT
  - **Route:** `/api/v1/rbac/roles/{roleId}/permissions/{permissionId}`
  - **Request / Công dụng:** Không body; gán quyền
  - **Thành công:** `204`
  - **Lỗi nghiệp vụ:** `404/409`

- **Method:** DELETE
  - **Route:** `/api/v1/rbac/roles/{roleId}/permissions/{permissionId}`
  - **Request / Công dụng:** Không body; gỡ quyền
  - **Thành công:** `204`
  - **Lỗi nghiệp vụ:** `404/409`

- **Method:** GET
  - **Route:** `/api/v1/rbac/users/{userId}/roles`
  - **Request / Công dụng:** Role của tài khoản
  - **Thành công:** `200 RoleDto[]`
  - **Lỗi nghiệp vụ:** `404`

- **Method:** PUT
  - **Route:** `/api/v1/rbac/users/{userId}/roles/{roleId}`
  - **Request / Công dụng:** Không body; gán role
  - **Thành công:** `204`
  - **Lỗi nghiệp vụ:** `404/409`

- **Method:** DELETE
  - **Route:** `/api/v1/rbac/users/{userId}/roles/{roleId}`
  - **Request / Công dụng:** Không body; gỡ role
  - **Thành công:** `204`
  - **Lỗi nghiệp vụ:** `404/409`

- **Method:** GET
  - **Route:** `/api/v1/rbac/audit-logs`
  - **Request / Công dụng:** Query `page=1&pageSize=20`; lịch sử thao tác
  - **Thành công:** `200 RbacPage<AuditDto>`
  - **Lỗi nghiệp vụ:** `400`


- RoleInput: `{ "roleName": "CUSTOM_OFFICER", "description": "Vai trò kiểm thử" }`. Tên tối đa 50, bắt đầu
  bằng chữ ASCII, chỉ chữ/số/underscore; chuẩn hóa hoa. Description tùy chọn tối đa 2000.
- RoleDto: `id, roleName, description?, isSystem, permissions: PermissionDto[]`.
- PermissionDto: `id, permissionCode, permissionName, module`.
- RbacPage: `items, page, pageSize, total`; pageSize 1–100.
- AuditDto: `id, actorUserId, action, targetUserId?, roleId?, permissionId?, details, createdAt`; `details`
  là **chuỗi JSON**, không phải object trực tiếp.

Năm role hệ thống: `REGISTERED_CITIZEN`, `FRONT_DESK_OFFICER`, `MANAGER`, `PROCEDURE_MANAGER`, `IT_ADMIN`
. Không đổi tên/xóa role hệ thống. Xóa role tùy chỉnh cần gỡ các liên kết trước. Không gỡ `iam.manage` khỏi
IT_ADMIN hoặc làm mất IT_ADMIN hoạt động cuối cùng. Gán lại liên kết đã có không tạo bản sao; gỡ liên kết
không có có thể trả 204 khi các đối tượng vẫn tồn tại.

Luồng cấp quyền: tài khoản đăng ký → IT_ADMIN lấy userId, roleId từ API danh sách → PUT gán role → tài khoản
nhận access token mới để dùng quyền ở service khác. IAM kiểm tra quyền hiện tại trong DB; Procedure dùng role
trong JWT, nên token cũ có thể chưa phản ánh thay đổi role. Chưa có API tạo/xóa Permission tùy ý hoặc API
bootstrap IT_ADMIN đầu tiên.

## 4. Procedure: tra cứu và quản trị thủ tục

### 4.1. Danh mục endpoint

Quyền **ProcedureManager** trong bảng nghĩa là Bearer chứa `PROCEDURE_MANAGER` hoặc `IT_ADMIN`. Role
`MANAGER` đơn thuần không đáp ứng policy này.

- **Method:** GET
  - **Route:** `/api/v1/procedures`
  - **Quyền / Request:** Public; bộ lọc và phân trang bên dưới
  - **Thành công:** `200 PagedResult<ProcedureSummaryDto>`
  - **Lỗi nghiệp vụ:** `400`

- **Method:** GET
  - **Route:** `/api/v1/procedures/{id}`
  - **Quyền / Request:** Public; chỉ thủ tục active
  - **Thành công:** `200 ProcedureDetailDto`
  - **Lỗi nghiệp vụ:** `404` nếu thiếu/inactive

- **Method:** GET
  - **Route:** `/api/v1/procedures/{id}/source`
  - **Quyền / Request:** Public; lấy link PDF gốc từ bản nháp đã xuất bản
  - **Thành công:** `200 {url,expiresInSeconds}`
  - **Lỗi nghiệp vụ:** `404/503`

- **Method:** GET
  - **Route:** `/api/v1/procedure-manager/procedures`
  - **Quyền / Request:** ProcedureManager; bộ lọc quản trị
  - **Thành công:** `200 PagedResult<ProcedureManagerSummaryDto>`
  - **Lỗi nghiệp vụ:** `400`

- **Method:** POST
  - **Route:** `/api/v1/procedure-manager/procedures`
  - **Quyền / Request:** ProcedureManager; ProcedureInput
  - **Thành công:** `201 ProcedureDetailDto`
  - **Lỗi nghiệp vụ:** `400/409`

- **Method:** PUT
  - **Route:** `/api/v1/procedure-manager/procedures/{id}`
  - **Quyền / Request:** ProcedureManager; UpdateProcedureInput
  - **Thành công:** `200 ProcedureDetailDto`
  - **Lỗi nghiệp vụ:** `400/404/409`

- **Method:** PATCH
  - **Route:** `/api/v1/procedure-manager/procedures/{id}/status`
  - **Quyền / Request:** ProcedureManager; `{isActive,reason?}`
  - **Thành công:** `200 ProcedureStatusDto`
  - **Lỗi nghiệp vụ:** `400/404`

- **Method:** GET
  - **Route:** `/api/v1/procedure-manager/procedures/{id}/versions`
  - **Quyền / Request:** ProcedureManager; không body
  - **Thành công:** `200 ProcedureVersionDto[]`
  - **Lỗi nghiệp vụ:** `404`

- **Method:** POST
  - **Route:** `/api/v1/procedure-manager/procedures/publish`
  - **Quyền / Request:** ProcedureManager; ReviewedProcedureInput
  - **Thành công:** `200 ProcedureDetailDto` khi thêm hoặc cập nhật
  - **Lỗi nghiệp vụ:** `400/409`


### 4.2. Tìm kiếm và phân trang

- **Query parameter:** `keyword`
  - **Mặc định / Ý nghĩa:** Tùy chọn, tối đa 255; tìm mã/tên, hỗ trợ không phân biệt hoa thường và dấu

- **Query parameter:** `categoryId`
  - **Mặc định / Ý nghĩa:** Tùy chọn; số nguyên dương

- **Query parameter:** `levelOfImplementation`
  - **Mặc định / Ý nghĩa:** Tùy chọn, tối đa 50; lọc cấp thực hiện. Không truyền thì ưu tiên cấp Xã/Phường,
    **không loại các cấp khác**

- **Query parameter:** `pageNumber`
  - **Mặc định / Ý nghĩa:** 1

- **Query parameter:** `pageSize`
  - **Mặc định / Ý nghĩa:** 10; tối đa 50 public, 100 manager

- **Query parameter:** `sortBy`
  - **Mặc định / Ý nghĩa:** `Title`; nhận `Title`, `ProcedureCode`, `UpdatedAt`, `CreatedAt`,
    `LevelOfImplementation`

- **Query parameter:** `isAscending`
  - **Mặc định / Ý nghĩa:** true

- **Query parameter:** `isActive`
  - **Mặc định / Ý nghĩa:** Chỉ manager: true/false; bỏ qua để lấy cả hai. Public luôn ép true

- **Query parameter:** `page`, `search`
  - **Mặc định / Ý nghĩa:** Alias tương thích; page ưu tiên hơn pageNumber; search dùng khi keyword không được
    truyền


Ví dụ public: `/api/v1/procedures?keyword=ket%20hon&pageNumber=1&pageSize=10`.
Ví dụ quản trị thủ tục đã tắt: `/api/v1/procedure-manager/procedures?isActive=false&pageNumber=1&pageSize=20`.

PagedResult có cấu trúc:

```json
{
  "items": [],
  "currentPage": 1,
  "pageSize": 10,
  "totalCount": 0,
  "totalPages": 0,
  "hasPrevious": false,
  "hasNext": false
}
```

ProcedureSummaryDto:
`id, procedureCode, title, categoryName, levelOfImplementation, feeSummary, processingTimeSummary, originalPdfUrl?, updatedAt`
.
ProcedureManagerSummaryDto bổ sung `isActive, versionCount, createdAt`. Danh sách không tải
ContentPayload/Checklist/FormDefinitions; dùng API chi tiết khi người dùng mở một thủ tục.

### 4.3. Payload tạo/cập nhật/xuất bản

Ví dụ dữ liệu **minh họa**, không đại diện quy định pháp luật. Thay categoryId bằng danh mục thực tế có trong
DB:

```json
{
  "categoryId": 1,
  "procedureCode": "DEMO.001",
  "title": "Thủ tục minh họa để kiểm thử",
  "issuingAuthority": "Cơ quan ban hành minh họa",
  "executingAgency": "Cơ quan tiếp nhận minh họa",
  "levelOfImplementation": "Cấp Xã",
  "targetAudience": "Công dân Việt Nam",
  "feeSummary": "Theo văn bản gốc",
  "processingTimeSummary": "Theo văn bản gốc",
  "originalPdfUrl": null,
  "pdfFileName": null,
  "contentPayload": {
    "decisionNumber": "DEMO/QD",
    "receivingAddress": "Địa điểm tiếp nhận minh họa",
    "submissionMethods": [{
      "methodName": "Trực tiếp", "feeAmount": 0, "feeUnit": "VND",
      "estimatedDays": 1, "note": "Số liệu kiểm thử"
    }],
    "legalReferences": [{
      "documentNumber": "DEMO/QD", "documentName": "Văn bản minh họa",
      "issueDate": "2026-01-01", "authority": "Cơ quan minh họa"
    }],
    "results": ["Kết quả minh họa"],
    "cases": [{
      "caseCode": "CASE_01", "caseName": "Trường hợp thông thường",
      "steps": [{
        "stepOrder": 1, "stepName": "Nộp hồ sơ", "executor": "Công dân",
        "actionDetails": "Chuẩn bị và gửi các giấy tờ theo danh sách."
      }]
    }]
  },
  "checklistSchema": [{
    "checklistId": "DOC_01", "caseCode": "CASE_01", "submissionType": "NOP",
    "itemName": "Tờ khai minh họa", "documentCopyType": "ORIGINAL",
    "quantity": 1, "conditionNote": null, "isMandatory": true
  }],
  "formDefinitions": [{
    "formTemplateId": null, "caseCode": "CASE_01", "formCode": "DEMO_FORM",
    "formName": "Tờ khai minh họa", "formType": "DOCX_TEMPLATE",
    "quantity": 1, "isMandatory": true
  }]
}
```

Đây là cấu trúc ProcedureInput/ReviewedProcedureInput. Khi PUT cập nhật, gửi toàn bộ dữ liệu mới, bổ sung **ở
cấp ngoài cùng** `decisionNumber` và `effectiveDate`, ví dụ `"effectiveDate": "2026-10-04"`. EffectiveDate
ghi metadata phiên bản, không tự lên lịch xuất bản.

Các giới hạn chính:

- categoryId phải tồn tại; procedureCode bắt buộc/tối đa 50, duy nhất; title từ 10 đến 500.
- issuingAuthority tối đa 255; levelOfImplementation 50; targetAudience, feeSummary, processingTimeSummary
  255. Gửi đầy đủ các trường bắt buộc khi xuất bản.
- originalPdfUrl nullable, HTTPS URL tuyệt đối không có userinfo, tối đa 500; pdfFileName nullable/tối đa 255.
  URL lưu không mặc nhiên là URL công khai tải được.
- ContentPayload gồm decisionNumber, receivingAddress, submissionMethods, legalReferences, results, cases. Mỗi
  case có nhiều steps; không cần ép mọi thủ tục vào một trường hợp duy nhất.
- feeAmount/estimatedDays không âm; stepOrder dương. Checklist/form quantity dương.
- submissionType: `NOP` hoặc `XUAT_TRINH`; documentCopyType: `ORIGINAL`, `CERTIFIED_COPY`, `REGULAR_COPY`.
- formType: `ONLINE_INTERACTIVE` hoặc `DOCX_TEMPLATE`. formTemplateId là ID tham chiếu Document Form, không
  phải khóa ngoại xuyên database.

ProcedureDetailDto gồm toàn bộ trường dữ liệu trên và `id, categoryName, isActive, createdAt, updatedAt`.
ProcedureStatusDto: `id, isActive, reason?, updatedAt`.
ProcedureVersionDto:
`id, versionNumber, decisionNumber?, effectiveDate, snapshotData, createdAt, originalPdfUrl?, pdfFileName?`;
snapshotData là **object JSON**. Danh sách version trả thứ tự phiên bản giảm dần.

### 4.4. Chọn API phù hợp

- **POST tạo**: yêu cầu mã chưa có, tạo active và version 1; trùng mã trả lỗi.
- **PUT cập nhật**: cập nhật theo ID; lưu snapshot trạng thái trước sửa vào version tiếp theo. Version 1 là
  trạng thái khởi tạo nên snapshot version 2 sau lần sửa đầu có thể giống version 1.
- **POST publish**: upsert theo procedureCode, thêm mới hoặc lưu snapshot cũ rồi cập nhật trong transaction.
  Thủ tục đã có giữ trạng thái active/inactive hiện tại; dùng PATCH status nếu cần mở lại. Gọi lặp có thể sinh
  thêm version, không coi API này là idempotent.
- **PATCH status**: bật/tắt tiếp nhận bằng isActive; reason tùy chọn. Không xóa dữ liệu.
- **Nguồn PDF public**: trả SAS URL có thời hạn 600 giây, chỉ khi thủ tục active và có bản nháp Published khớp
  nguồn PDF hiện tại. PDF nhập tay chỉ có URL mà không có bản nháp tương ứng có thể trả 404. SAS đã cấp còn
  hiệu lực đến lúc hết hạn.

Hiện chưa có DELETE thủ tục. Đã bổ sung CRUD lĩnh vực và GET chi tiết quản trị (API 66, 68–70). Không dùng
public detail để xem thủ tục inactive vì sẽ nhận 404.

## 5. Procedure: PDF, bản nháp và đối soát

Các endpoint draft yêu cầu ProcedureManager; DELETE mới xem API 67, nguồn PDF lịch sử xem API 73. Backend cung cấp dữ liệu cho màn hình đối soát; không có màn
hình FE Split-view trong backend.

- **Method:** POST
  - **Route:** `/api/v1/procedure-manager/drafts/extract-preview`
  - **Request / Công dụng:** Multipart `file`; đọc thử PDF, không lưu DB/Blob
  - **Thành công:** `200 ExtractionResult`
  - **Lỗi điển hình:** `400/413/502/503/504`

- **Method:** POST
  - **Route:** `/api/v1/procedure-manager/drafts`
  - **Request / Công dụng:** Multipart `file`; lưu PDF và tạo draft
  - **Thành công:** `202 DraftDto`, Location tới draft
  - **Lỗi điển hình:** `400/413/503`

- **Method:** GET
  - **Route:** `/api/v1/procedure-manager/drafts`
  - **Request / Công dụng:** Query `page=1&pageSize=10`
  - **Thành công:** `200 DraftSummaryDto[]`
  - **Lỗi điển hình:** `400`

- **Method:** GET
  - **Route:** `/api/v1/procedure-manager/drafts/{id}`
  - **Request / Công dụng:** Lấy dữ liệu đối soát
  - **Thành công:** `200 DraftDto`
  - **Lỗi điển hình:** `404`

- **Method:** PUT
  - **Route:** `/api/v1/procedure-manager/drafts/{id}`
  - **Request / Công dụng:** `{revision,payload}`; lưu phần đã chỉnh sửa
  - **Thành công:** `200 DraftDto`
  - **Lỗi điển hình:** `400/404/409`

- **Method:** POST
  - **Route:** `/api/v1/procedure-manager/drafts/{id}/retry`
  - **Request / Công dụng:** `{revision}`; chạy lại draft Failed
  - **Thành công:** `200 DraftDto`
  - **Lỗi điển hình:** `404/409/503`

- **Method:** POST
  - **Route:** `/api/v1/procedure-manager/drafts/{id}/publish`
  - **Request / Công dụng:** `{revision,confirmed:true}`
  - **Thành công:** `200 ProcedureDetailDto`
  - **Lỗi điển hình:** `400/404/409`

- **Method:** GET
  - **Route:** `/api/v1/procedure-manager/drafts/{id}/source`
  - **Request / Công dụng:** Link PDF phục vụ đối soát
  - **Thành công:** `200 {url,expiresInSeconds:600}`
  - **Lỗi điển hình:** `404/503`


PDF phải có đuôi .pdf và nội dung PDF hợp lệ, tối đa 20 MiB. Preview cần kết nối AIOCR nhưng không cần Blob;
upload draft cần Blob. Danh sách draft trả **mảng trực tiếp**, không có totalCount; pageSize tối đa 50.

### 5.1. DTO và trạng thái

- ExtractionResult: `payload` (object dữ liệu thủ tục gợi ý), `extractedText` (text gốc), `warnings` (mảng
  chuỗi).
- DraftDto:
  `id, status, pdfFileName, payload, warnings, extractedText, failureCode?, revision, publishedProcedureId?, createdAt, updatedAt`
  .
- DraftSummaryDto:
  `id, status, pdfFileName, failureCode?, revision, publishedProcedureId?, createdAt, updatedAt`.
- status: `Queued` → `Processing` → `NeedsReview` hoặc `Failed`; sau xác nhận thành công là `Published`.
- revision là GUID kiểm soát sửa đồng thời. Luôn gửi revision mới nhất từ GET/PUT; gặp 409 cần tải lại và đối
  chiếu trước khi gửi tiếp.
- PUT payload có giới hạn 500.000 ký tự JSON và request tối đa 1 MiB. Có thể chỉnh draft NeedsReview/Failed;
  không sửa draft đang xử lý hoặc đã xuất bản.

### 5.2. Luồng đọc PDF thuận tiện

**Đọc thử không lưu:** chọn file → POST extract-preview → hiển thị extractedText và warnings → người dùng sửa
payload. Nếu AI tắt, vẫn có thể nhận text PDF nhưng payload chưa được điền tự động. Preview không sinh
draftId, không lưu PDF gốc.

**Lưu và xuất bản có PDF gốc:** POST drafts → lưu draftId → GET draft cho đến trạng thái cần đối soát → GET
source hiển thị PDF → PUT payload đã sửa kèm revision → POST draft publish kèm revision mới và confirmed=true.
Xuất bản cập nhật procedure/version và trạng thái draft trong transaction. PDF metadata được backend lấy từ
nguồn draft; không tin URL tùy ý FE gửi trong payload. Draft đã Published không xuất bản lần hai.

**Chế độ thủ công:** nếu `ProcedureDrafts:ExtractionEnabled=false`, upload đi vào NeedsReview với payload chờ
nhập tay. Có thể đọc thử PDF qua preview riêng rồi điền dữ liệu để đối soát. Chỉ có text không đồng nghĩa toàn
bộ trường nghiệp vụ đã được tự động điền chính xác.

**Chế độ tự động:** cấu hình AIOCR URL/service key và bật ExtractionEnabled. AIOCR ưu tiên trích chữ có sẵn
trong PDF; OCR chỉ dành cho trang scan/thiếu text khi được bật. AI ánh xạ text thành payload là bước riêng.
Người quản lý vẫn phải xem warnings và đối soát trước publish.

Ví dụ upload bằng FE:

```javascript
const data = new FormData();
data.append('file', selectedPdfFile);
const response = await fetch(`${apiBase}/api/v1/procedure-manager/drafts`, {
  method: 'POST',
  headers: { Authorization: `Bearer ${accessToken}` },
  body: data
});
if (!response.ok) throw await response.json();
const draft = await response.json();
// Giữ draft.id và draft.revision để gọi các bước đối soát tiếp theo.
```

FE không nhận hoặc gửi X-Service-Key. Không lưu SAS URL lâu dài; gọi lại source khi URL hết hạn.
Source/preview được trả với chỉ dẫn không cache.

## 6. Document Form: FE tích hợp editor DOCX

Base local qua Gateway: `http://localhost:5000/api/document-form`.
Swagger: `http://localhost:5000/api/document-form/swagger/index.html`.
Các route API bên dưới nối tiếp base bằng `/api/v1/...`.

1. **Admin / Quản lý mẫu:** POST tạo code/title → upload-docx multipart → GET chi tiết thấy onlineReady=true.
2. **Chi tiết thủ tục:** service Procedure trả thành phần giấy tờ có templateId; FE hiển thị nút “Điền đơn”.
3. **Mở đơn mới:** GET template, GET template/download-docx → đọc arrayBuffer/blob → editor import DOCX.
   Đây là tải dữ liệu vào ứng dụng web, không bắt người dân tự tải file rồi mở Word desktop.
4. **Lưu nháp:** editor export DOCX → POST draft multipart; giữ submissionId từ response.
5. **Sửa tiếp:** GET submission metadata + GET submission/download-docx → import editor; PUT draft multipart khi lưu.
6. **Hoàn thành:** chờ PUT/POST lưu nháp thành công → POST submit. FE hiển thị “Hoàn thành đơn”, editor read-only.
   submissionId gắn vào thành phần hồ sơ ở Workflow; giấy tờ khác vẫn thuộc luồng riêng.

Ví dụ adapter FE (editor.importDocx/exportDocx là hàm minh họa, thay bằng API của editor đã chọn):

```javascript
const base = "http://localhost:5000/api/document-form";
async function checked(response) {
  if (!response.ok) throw new Error(await response.text());
  return response;
}
async function openOriginal(templateId, editor) {
  const response = await checked(await fetch(`${base}/api/v1/form-templates/${templateId}/download-docx`));
  await editor.importDocx(await response.arrayBuffer());
}
async function saveDraft(editor, templateId, applicantId, submissionId) {
  const data = new FormData();
  data.append("applicantId", applicantId);
  if (!submissionId) data.append("templateId", templateId);
  data.append("file", await editor.exportDocx(), "don-da-dien.docx");
  const route = submissionId ? `${submissionId}/draft` : "draft";
  const response = await checked(await fetch(`${base}/api/v1/citizen/submissions/${route}`, {
    method: submissionId ? "PUT" : "POST", body: data
  })); // Không tự đặt Content-Type: trình duyệt thêm multipart boundary.
  return response.json();
}
```

Đừng mở lại mẫu admin khi người dân sửa một submission đã có: sẽ làm mất nội dung đã nhập trên màn hình.
Đừng gửi JSON formData hoặc đường dẫn file thay cho binary. Không chỉnh trực tiếp Blob mẫu của admin.
Editor FE phải hỗ trợ import/export DOCX; backend bảo toàn bytes nhận được nhưng không đảm bảo editor render
mọi font/bảng/ngắt trang giống Word. Kiểm tra hiển thị với file mẫu thực tế ở FE là bước tích hợp riêng.

Test local: `python scripts/verify-documentform-online.py http://localhost:5000/api/document-form`.
Script tạo dữ liệu tổng hợp riêng với code DOCX-* và applicantId ngẫu nhiên; không gọi Azure.
Có thể truyền đường dẫn DOCX làm đối số thứ hai để kiểm tra round-trip trên mẫu thật.

## 7. AIOCR: API nội bộ

- **Method:** POST
  - **Route:** `/internal/v1/procedure-extractions`
  - **Xác thực / Request:** `X-Service-Key`; body **bytes PDF**, `Content-Type: application/pdf`
  - **Thành công:** `200 ExtractionResult`
  - **Lỗi điển hình:** `400/401/413/502/503/504`


Đây là hợp đồng backend-to-backend, **không phải multipart** và không dùng JWT người dùng. FE gọi
preview/drafts ở Procedure thay vì gọi API này. Gateway hiện có proxy prefix AIOCR; vì vậy không suy ra
“internal” trong route đồng nghĩa không thể truy cập qua mạng ngoài. Service vẫn kiểm tra key.

Cấu hình cần biết:

- AIOCR: `Extraction:Enabled`, `ServiceAuthentication:Key` (key đủ mạnh, ít nhất 32 ký tự).
- Procedure: `ProcedureDrafts:AiOcrUrl`, `ProcedureDrafts:ServiceKey` khớp key AIOCR; `ExtractionEnabled`
  điều khiển xử lý draft nền.
- `Extraction:UseAI` quyết định có ánh xạ text bằng AI; `OcrFallbackEnabled` quyết định có dùng OCR khi không
  đủ text. Các bước cloud cần cấu hình dịch vụ tương ứng; không đặt secret trong FE.

Native extraction đọc text PDF trước, có giới hạn 100 trang và 100.000 ký tự text, upload tối đa 20 MiB. PDF
scan/thiếu text có thể chỉ trả cảnh báo để đối soát khi OCR tắt. Khi AI tắt, kết quả có extractedText/warnings
và payload cần hoàn thiện thủ công. Không coi HTTP 200 là cam kết payload đã đủ điều kiện publish.

## 8. Health, Swagger và service chưa có nghiệp vụ

- **Service:** IAM, Procedure, ApplicationWorkflow, AIOCR, Analytics
  - **Method / Route:** `GET /` tại từng base URL
  - **Response:** `200 {service}`

- **Service:** DocumentForm
  - **Method / Route:** `GET /`
  - **Response:** `200 {service,version,status}`

- **Service:** Sáu service trên
  - **Method / Route:** `GET /health`
  - **Response:** Health check, thông thường `200` text `Healthy`

- **Service:** Gateway
  - **Method / Route:** `GET /health`
  - **Response:** Health check Gateway


Có 6 root endpoint và 7 health endpoint theo host. Health hiện không chứng minh PostgreSQL/Blob/AI đều sẵn
sàng vì chưa có kiểm tra dependency tương ứng được đăng ký. Gateway không có root endpoint nghiệp vụ.

IAM, Procedure và DocumentForm bật Swagger trong Development: `GET /swagger/index.html` (giao diện),
`GET /swagger/v1/swagger.json` (OpenAPI). `/swagger` thường chuyển hướng vào giao diện. Không mặc định Swagger
có trên Production.

ApplicationWorkflow đã có controller hồ sơ/cán bộ (xem mục TASK-11 bên dưới); AnalyticsSystem mới có root/health. Không có endpoint
import CSV trong danh mục này.

## 9. Xử lý lỗi và trình tự tích hợp FE

### 9.1. Lỗi

IAM/Procedure thường dùng `application/problem+json` với status/title/instance, code/traceId và errors khi
validation. Ví dụ minh họa:

```json
{
  "title": "Dữ liệu không hợp lệ.",
  "status": 400,
  "instance": "/api/v1/procedure-manager/procedures",
  "code": "validation.failed",
  "traceId": "example-trace",
  "errors": { "Title": ["Tên thủ tục không hợp lệ."] }
}
```

Không dựa vào nội dung câu thông báo để điều khiển FE; ưu tiên HTTP status và code. IAM dùng
`validation_failed`, Procedure dùng `validation.failed` ở luồng validation nghiệp vụ. Các lỗi thường gặp:
`iam.csrf_rejected`, `iam.profile_not_found`, `iam.profile_exists`, `iam.self_disable`, `iam.last_admin`,
`procedure.not_found`, `procedure.category_not_found`, `procedure.code_exists`.

DocumentForm hiện có lỗi nghiệp vụ dạng `{code,message}`, lỗi model validation là ValidationProblemDetails;
không phải mọi lỗi ở mọi service đều cùng cấu trúc. FE nên hỗ trợ cả `title`, `message`, `errors`, và
response không có JSON nếu lỗi từ proxy.

- **HTTP:** 400
  - **Hành động phù hợp:** Hiển thị lỗi theo trường, sửa dữ liệu

- **HTTP:** 401
  - **Hành động phù hợp:** Thử refresh một lần theo luồng IAM; thất bại thì yêu cầu đăng nhập

- **HTTP:** 403
  - **Hành động phù hợp:** Kiểm tra quyền; riêng auth kiểm tra CSRF/Origin

- **HTTP:** 404
  - **Hành động phù hợp:** Đối tượng không tồn tại hoặc bị ẩn khỏi public

- **HTTP:** 409
  - **Hành động phù hợp:** Trùng mã, trạng thái không cho phép hoặc revision cũ; tải lại trước khi sửa

- **HTTP:** 413
  - **Hành động phù hợp:** Giảm kích thước upload/request

- **HTTP:** 502/503/504
  - **Hành động phù hợp:** Kiểm tra dịch vụ phụ thuộc/cấu hình/timeout; giữ dữ liệu người dùng đang chỉnh


### 9.2. Trình tự thử trên Swagger/Postman

1. Chạy service và dependency cần dùng; mở Swagger của đúng service. Cổng trong mục 1 là cấu hình, không khẳng
định tiến trình đang chạy.
2. IAM: register → login với X-CSRF-Protection → giữ cookie trong trình duyệt/cookie jar → Authorize access
token → GET me/profile. Khi gọi bằng Postman, bật lưu/gửi cookie đúng host/path.
3. IT_ADMIN: GET accounts/roles → gán PROCEDURE_MANAGER cho tài khoản kiểm thử → lấy token mới. Không có tài
khoản IT_ADMIN thì cần người vận hành cấp tài khoản ban đầu.
4. Procedure public: GET danh sách → GET chi tiết. Procedure manager: tạo/publish payload → kiểm tra versions
→ tắt status → xác nhận public không còn trả thủ tục đó nhưng manager vẫn thấy.
5. PDF: dùng extract-preview để kiểm tra đọc chữ; dùng drafts nếu cần lưu PDF, đối soát và xuất bản có nguồn
gốc. Chỉ nhấn publish khi dữ liệu được kiểm tra đầy đủ.
6. DocumentForm: tạo template → upload DOCX nguyên bản → FE import/export DOCX → lưu file nháp → chốt đơn. Xem mục 3 và phụ lục 6 cho hợp đồng online mới.

### 9.3. Đối chiếu mã nguồn khi API thay đổi

Nguồn chính là các thư mục `Controllers` và `Program.cs` của từng API, DTO/validator tầng Application, cấu
hình YARP và security IAM. Tài liệu này không thay đổi endpoint hoặc quyền. Khi thêm API, cần bổ sung
method/route, policy, DTO, status và luồng sử dụng tại đây để FE có một đầu mối tra cứu.


### Khắc phục lỗi DocumentForm local (07/10/2026)

Compose `docker/docker-compose.yml` dùng database riêng `wardmate_documentform_db`. Nếu log có
`created_at_utc does not exist` hoặc migration báo `application_forms already exists`, container đang
trỏ vào schema cũ trong `wardmate_db`; không xóa bảng hay ghi giả lịch sử migration để bỏ qua lỗi.
Khởi tạo database riêng, áp migration chuẩn và giữ database cũ để đối chiếu. Azurite chạy ngoài container
API nên không dùng loopback của API; mặc định Docker Desktop dùng DevelopmentStorageProxyUri
`http://host.docker.internal`. Có thể override bằng AZURE_BLOB_CONNECTION_STRING.

Luồng editor mới không dùng checksum/schema cũ. Không cần cấu hình online-config trước khi tạo draft.
Tên file có dấu/khoảng trắng được giải mã URL khi tải lại từ Blob. Backend lưu/tải nguyên bytes DOCX
đã nhận, không thêm trang formData và không ghi đè mẫu khi người dân lưu đơn.
## Bổ sung RBAC — Danh mục chức năng đã deploy (07/10/2026)

Database IAM deploy đã được bổ sung 27 permission bằng migration DeployedFeaturePermissionCatalog,
đưa tổng số permission lên 30. Không xóa/sửa ba permission cũ hoặc thu hồi các role-permission đã có.
Không tạo quyền cho các chức năng chưa xác nhận deploy như Analytics, SMS, RAG hoặc phê duyệt toàn hồ sơ.

Admin gọi GET /api/v1/rbac/permissions để lấy danh mục, GET /api/v1/rbac/roles/{roleId}/permissions
để xem quyền của role. Dùng các API gán/gỡ permission hiện có; không đoán permissionId, lấy ID từ response.
Tên quyền mới là tiếng Việt, module là IAM, ProcedureCatalog hoặc DocumentForm.

**Quan trọng về hiệu lực:** Migration này cập nhật danh mục quyền và phân quyền mặc định trong IAM,
không tự đổi authorization của các service. IAM vẫn dùng policy hiện có và phạm vi phường ở backend.
Procedure hiện dùng role PROCEDURE_MANAGER/IT_ADMIN; gỡ một permission procedure.* chưa chặn riêng endpoint.
DocumentForm đang nhận applicantId từ request và chưa tích hợp JWT theo code được kiểm tra; các permission
DocumentForm mới chưa khắc phục vấn đề đó. Không dùng việc ẩn menu ở FE thay cho bảo vệ API.

Access token đã phát hành không được sửa nội dung. Đăng nhập lại hoặc refresh qua IAM để nhận claim
permissions cập nhật; GET /api/v1/users/me dùng để tải lại thông tin quyền cho giao diện.
Dữ liệu public như danh sách thủ tục/danh mục vẫn là public, không yêu cầu thêm permission.
Quyền document.submissions.* mô tả thao tác trên đơn của chính mình, không cấp quyền xem đơn người khác.

Danh mục bổ sung và role được gán mặc định:
- `iam.accounts.read` — Xem tài khoản và hồ sơ trong phạm vi quản lý.
  Role: IT_ADMIN, MANAGER.

- `iam.accounts.manage` — Quản lý tài khoản và hồ sơ trong phạm vi được phép.
  Role: IT_ADMIN, MANAGER.

- `iam.wards.read` — Xem phường trong phạm vi quản lý.
  Role: IT_ADMIN, MANAGER.

- `iam.wards.manage` — Tạo phường và phân công phường công tác.
  Role: IT_ADMIN.

- `iam.rbac.manage` — Quản trị vai trò và quyền toàn hệ thống.
  Role: IT_ADMIN.

- `iam.audit.read` — Xem nhật ký phân quyền.
  Role: IT_ADMIN.

- `procedure.read` — Xem toàn bộ thủ tục, kể cả đã ngừng công khai.
  Role: IT_ADMIN, PROCEDURE_MANAGER.

- `procedure.create` — Tạo thủ tục.
  Role: IT_ADMIN, PROCEDURE_MANAGER.

- `procedure.update` — Cập nhật nội dung thủ tục.
  Role: IT_ADMIN, PROCEDURE_MANAGER.

- `procedure.publish` — Xuất bản dữ liệu thủ tục đã đối soát.
  Role: IT_ADMIN, PROCEDURE_MANAGER.

- `procedure.status` — Bật hoặc tắt thủ tục.
  Role: IT_ADMIN, PROCEDURE_MANAGER.

- `procedure.versions.read` — Xem lịch sử phiên bản thủ tục.
  Role: IT_ADMIN, PROCEDURE_MANAGER.

- `procedure.rollback` — Khôi phục phiên bản thủ tục.
  Role: IT_ADMIN, PROCEDURE_MANAGER.

- `procedure.source.read` — Đọc PDF gốc của phiên bản lịch sử.
  Role: IT_ADMIN, PROCEDURE_MANAGER.

- `procedure.categories.manage` — Thêm sửa xóa danh mục thủ tục.
  Role: IT_ADMIN, PROCEDURE_MANAGER.

- `procedure.drafts.read` — Xem bản nháp và PDF đối soát.
  Role: IT_ADMIN, PROCEDURE_MANAGER.

- `procedure.drafts.upload` — Tải PDF tạo bản nháp.
  Role: IT_ADMIN, PROCEDURE_MANAGER.

- `procedure.drafts.update` — Sửa dữ liệu bản nháp.
  Role: IT_ADMIN, PROCEDURE_MANAGER.

- `procedure.drafts.extract` — Đọc thử PDF và thử trích xuất lại.
  Role: IT_ADMIN, PROCEDURE_MANAGER.

- `procedure.drafts.publish` — Xác nhận và xuất bản bản nháp.
  Role: IT_ADMIN, PROCEDURE_MANAGER.

- `procedure.drafts.delete` — Xóa bản nháp chưa xuất bản.
  Role: IT_ADMIN, PROCEDURE_MANAGER.

- `document.templates.read` — Tra cứu và tải biểu mẫu gốc.
  Role: IT_ADMIN, PROCEDURE_MANAGER, MANAGER, FRONT_DESK_OFFICER, REGISTERED_CITIZEN.

- `document.templates.manage` — Tạo biểu mẫu và tải DOCX gốc.
  Role: IT_ADMIN, PROCEDURE_MANAGER.

- `document.submissions.read` — Xem đơn điện tử của chính mình.
  Role: IT_ADMIN, PROCEDURE_MANAGER, MANAGER, FRONT_DESK_OFFICER, REGISTERED_CITIZEN.

- `document.submissions.write` — Tạo và sửa nháp đơn điện tử của chính mình.
  Role: IT_ADMIN, PROCEDURE_MANAGER, MANAGER, FRONT_DESK_OFFICER, REGISTERED_CITIZEN.

- `document.submissions.submit` — Chốt đơn điện tử của chính mình.
  Role: IT_ADMIN, PROCEDURE_MANAGER, MANAGER, FRONT_DESK_OFFICER, REGISTERED_CITIZEN.

- `document.submissions.download` — Tải DOCX đã điền của chính mình.
  Role: IT_ADMIN, PROCEDURE_MANAGER, MANAGER, FRONT_DESK_OFFICER, REGISTERED_CITIZEN.


## Application Workflow — TASK-11: thẩm định, yêu cầu sửa và nộp lại

Swagger local: `http://localhost:5003/swagger` khi service chạy ở Development.
Gateway chuyển tiếp nguyên route `/api/v1/applications/...` và
`/api/v1/officer/applications/...` đến Workflow.
Tất cả API dưới đây dùng `Authorization: Bearer <accessToken>` của IAM.

### Luồng sử dụng

1. Công dân tạo hồ sơ, hoàn tất checklist rồi gọi API `submit` hiện có.
2. Lần nộp đầu tạo snapshot version 1 gồm form và toàn bộ checklist/file URL.
3. Cán bộ Một cửa mở danh sách chờ và tự tiếp nhận bằng `assign`.
4. Cán bộ xem version, tạo các nhận xét cụ thể, rồi gọi `request-revision`.
5. Công dân đọc nhận xét, chỉnh dữ liệu trên FE và gửi toàn bộ dữ liệu qua `resubmit`.
6. Backend tạo version kế tiếp, đóng nhận xét của phiên bản trước, chuyển về SUBMITTED.
   Cán bộ cũ tiếp nhận lại và dùng `diff` để đối chiếu.

Phạm vi quyền: các API `/officer/...` yêu cầu role `FRONT_DESK_OFFICER`.
Cán bộ không được tự thẩm định hồ sơ của chính mình, không nhận hồ sơ đang thuộc cán bộ khác.
API đọc comments/versions/diff chỉ cho chủ hồ sơ hoặc cán bộ đang thụ lý.
IT_ADMIN/MANAGER không tự động có quyền thẩm định trong task này.
JWT role được kiểm tra tại service; task này không thêm permission vào IAM deploy.

Hiện Application chưa có phường tiếp nhận và JWT chưa cấp phạm vi phường cho luồng này.
Danh sách chờ gồm hồ sơ chưa phân công và hồ sơ của chính cán bộ, chưa chia theo phường.
Không được hiểu đây là cơ chế cách ly hồ sơ giữa các phường đã hoàn chỉnh.

### Thay đổi tương thích của API hồ sơ hiện có

- `POST /api/v1/applications`: vẫn nhận `{procedureId, caseCode?, formData}` và trả 201.
  Theo yêu cầu ApplicationCode NOT NULL, mã HS nay được cấp ngay khi tạo DRAFT.
  Mã không đổi khi submit/nộp lại; có thể có khoảng trống trong dãy mã.
- `POST /api/v1/applications/{id}/submit`: vẫn trả 200, yêu cầu DRAFT và checklist
  bắt buộc COMPLETED; nay lưu snapshot version 1 cùng transaction với lịch sử.
- `GET /api/v1/applications/{id}`: chủ hồ sơ đọc được `assignedOfficerId`,
  `resubmitCount`, `currentVersionNumber`, `notes`, `approvedAt` bổ sung.
  DRAFT có currentVersionNumber = 0. Không chuyển form/checklist thành dữ liệu dùng chung.
- API PATCH checklist cũ vẫn chỉ sửa DRAFT. Khi NEED_REVISION, FE gửi dữ liệu sửa
  bằng resubmit để form và checklist được kiểm tra/lưu cùng nhau.
- `userId` hiện có chính là CitizenId trong đặc tả; `history[].changedBy` là người
  chuyển trạng thái. Giữ tên cũ để không phá hợp đồng FE.

### GET /api/v1/officer/applications/pending

**Dùng để làm gì:** danh sách công việc chờ của cán bộ, hồ sơ nộp lâu nhất đứng trước.

**Cách gọi:** không có body. Query tùy chọn `status=SUBMITTED` hoặc `UNDER_REVIEW`,
`procedureId`, `submittedFrom`, `submittedTo` (ISO 8601 có múi giờ), `page=1`, `pageSize=20`.
Không truyền status lấy cả hai. Khoảng thời gian gồm hai đầu mút; nộp lại dùng thời điểm nộp mới.

**Kết quả:** 200 `{items, page, pageSize, total}`; items là thông tin gọn, không có form/file.
Trang rỗng vẫn 200. `pageSize` 1–100, page 1–1000000.

**Dùng sai:** 400 nếu status ngoài hai giá trị, phân trang sai hoặc ngày bắt đầu sau ngày kết thúc.
401 cần đăng nhập lại; 403 cần đúng role. Không dùng API này để lấy hồ sơ đã duyệt.

### POST /api/v1/officer/applications/{id}/assign

**Dùng để làm gì:** cán bộ tự nhận xử lý một hồ sơ SUBMITTED, chuyển sang UNDER_REVIEW.

**Cách gọi:** không body; cán bộ được lấy từ `sub` của JWT, không nhận officerId từ FE.

**Kết quả:** 200 `ReviewResultDto`, gồm `application`, `assignedOfficerId`,
`resubmitCount`, `currentVersionNumber`. Ghi lịch sử chuyển trạng thái cùng transaction.

**Dùng sai:** 404 không có hồ sơ; 403 tự nhận hồ sơ của mình/thiếu role;
409 hồ sơ đã được nhận, đang trạng thái khác hoặc không có snapshot hợp lệ.
Tải lại danh sách khi 409. Hai cán bộ nhận đồng thời chỉ một người thành công.

### POST /api/v1/officer/applications/{id}/comments

**Dùng để làm gì:** đánh dấu chính xác ô nhập hoặc giấy tờ cần sửa trên version hiện tại.

**Cách gọi:** cán bộ đang thụ lý hồ sơ UNDER_REVIEW gửi:

```json
{
  "versionNumber": 1,
  "targetType": "FORM_FIELD",
  "targetId": "/formData/fullName",
  "fieldLabel": "Họ và tên",
  "commentText": "Vui lòng nhập họ tên đúng theo giấy tờ."
}
```

FORM_FIELD dùng đường dẫn JSON Pointer: `/formData/address/city`,
`/formData/children/0/name`. Ký tự `/` trong tên key viết `~1`, `~` viết `~0`.
Đích phải tồn tại trong snapshot và là giá trị lá (hoặc object/array rỗng).
CHECKLIST_ITEM dùng UUID dòng checklist từ `application.checklists[].id`, không dùng code giấy tờ.
TargetId tối đa 100 ký tự; label 255; commentText 4000, đều không rỗng.

**Kết quả:** 200 ReviewResultDto; nhận xét OPEN gắn với phiên bản hiện tại.
Gọi GET comments để lấy id/nội dung nhận xét vừa tạo.

**Dùng sai:** 400 ô/giấy tờ không tồn tại hoặc nội dung sai;
403 không phải cán bộ thụ lý; 409 trạng thái/version đã thay đổi. Tải lại versions trước khi bắt lỗi.
Không dùng version cũ để đánh dấu dữ liệu mới.

### POST /api/v1/officer/applications/{id}/request-revision

**Dùng để làm gì:** gửi yêu cầu chỉnh sửa sau khi đã tạo nhận xét chi tiết.

**Cách gọi:** cán bộ thụ lý gửi `{"reason":"Vui lòng bổ sung giấy tờ và sửa các ô đã đánh dấu."}`.
Reason bắt buộc, tối đa 4000 ký tự.

**Kết quả:** 200 ReviewResultDto, NEED_REVISION; reason lưu vào notes và lịch sử.

**Dùng sai:** 409 nếu chưa có nhận xét OPEN của version hiện tại hoặc không còn UNDER_REVIEW.
Tạo nhận xét trước. 403 nếu người gọi không thụ lý hồ sơ; 400 nếu lý do rỗng.

### POST /api/v1/applications/{id}/resubmit

**Dùng để làm gì:** chủ hồ sơ gửi dữ liệu đã sửa trong một lần nộp lại.

**Cách gọi:** chỉ ở NEED_REVISION. Gửi toàn bộ formData mới và mọi dòng checklist,
bao gồm cả dòng tùy chọn và dòng không đổi:

```json
{
  "expectedVersionNumber": 1,
  "formData": { "fullName": "Nguyễn Văn A" },
  "checklists": [
    {
      "id": "11111111-1111-1111-1111-111111111111",
      "status": "COMPLETED",
      "fileUrl": "https://example.invalid/giay-to-da-sua.pdf",
      "note": "Đã thay giấy tờ"
    }
  ]
}
```

Dùng id thật của hồ sơ; ví dụ trên chỉ minh họa. Status PENDING hoặc COMPLETED.
FileUrl HTTPS tối đa 500 ký tự, hoặc null để bỏ liên kết; note tối đa 4000 ký tự.
FormData là object tối đa 64 KiB. Checklist rỗng `[]` chỉ khi hồ sơ không có checklist.
Không thể thay đổi tiêu đề, tính bắt buộc hay thêm/bỏ checklist trong API này.

**Kết quả:** 200 ReviewResultDto. ResubmitCount tăng một; snapshot version kế tiếp
được tạo; nhận xét OPEN của version trước thành RESOLVED; hồ sơ về SUBMITTED,
giữ cán bộ cũ và mã HS. RESOLVED có nghĩa đã nộp lại, chưa phải cán bộ duyệt nội dung sửa.
Toàn bộ thao tác cùng transaction, không có trường hợp lưu nửa chừng.

**Dùng sai:** 404 hồ sơ không thuộc bạn; 409 sai trạng thái hoặc expectedVersionNumber cũ;
400 thiếu/trùng checklist, form không đúng hoặc URL sai; 422 checklist bắt buộc chưa COMPLETED,
response `missingItems` chỉ ra giấy tờ cần hoàn tất. Không được chỉ gửi các ô đã sửa:
các ô bị bỏ khỏi formData sẽ được xem là bị xóa và hiển thị trong diff.

API chưa kiểm chứng nội dung file hay liên kết DocumentForm submission; COMPLETED là trạng thái
đã khai báo. Không xem đây là bằng chứng giấy tờ đã được xác thực.

### GET /api/v1/applications/{id}/comments

**Dùng để làm gì:** công dân biết cần sửa gì; cán bộ xem các nhận xét của mình trên hồ sơ.

**Cách gọi:** không body. Query `versionNumber=1`, `status=OPEN` hoặc `RESOLVED` tùy chọn.
Không truyền bộ lọc sẽ nhận toàn bộ nhận xét của hồ sơ.

**Kết quả:** 200 mảng CommentDto: id, applicationVersionId, versionNumber, officerId,
targetType, targetId, fieldLabel, commentText, status, createdAt, updatedAt.
Không có nhận xét phù hợp trả `[]`.

**Dùng sai:** 404 ngoài phạm vi truy cập hoặc version không thuộc hồ sơ;
400 số phiên bản/status sai. Nhận xét cũ vẫn còn để đối chiếu, không bị xóa khi nộp lại.

### GET /api/v1/applications/{id}/versions

**Dùng để làm gì:** lấy dữ liệu đóng băng cho màn hình xem hồ sơ và so sánh các lần nộp.

**Cách gọi:** không body; chủ hồ sơ hoặc cán bộ thụ lý. Cán bộ cần assign trước khi đọc.

**Kết quả:** 200 mảng VersionDto tăng dần theo versionNumber, gồm id, versionNumber,
submittedBy, submittedAt và snapshotData (thông tin hồ sơ, formData, checklists/file URL).
DRAFT chưa nộp trả `[]`. Snapshot không đổi khi các lần nộp sau sửa dữ liệu.

**Dùng sai:** 404 hồ sơ không tồn tại hoặc không thuộc phạm vi. Không dùng thời điểm updatedAt
của hồ sơ thay cho submittedAt của phiên bản; không gửi snapshotData vào API sửa trực tiếp.

### GET /api/v1/applications/{id}/diff?fromVersion=1&toVersion=2

**Dùng để làm gì:** xem những ô/giấy tờ đã đổi giữa hai lần nộp.

**Cách gọi:** chọn hai số phiên bản của cùng hồ sơ; toVersion lớn hơn fromVersion.
Chủ hồ sơ và cán bộ thụ lý được đọc.

**Kết quả:** 200 mảng các thay đổi: fieldKey, fieldLabel, oldValue, newValue,
hasChanged=true, associatedComments, oldExists, newExists.
Không có thay đổi trả `[]`. Diff đi sâu vào object/array của form;
checklist so theo UUID, không theo vị trí trong mảng. File URL đổi có fieldKey
`/checklists/{uuid}/fileUrl`. Giá trị bị xóa được phân biệt với giá trị null qua cờ Exists.
FieldLabel dùng nhãn nhận xét khi có, nếu không dùng đường dẫn fieldKey.
AssociatedComments chỉ gồm nhận xét thuộc hai phiên bản đang đối chiếu.

**Dùng sai:** 400 số phiên bản sai/thứ tự sai; 404 thiếu một phiên bản hoặc không có quyền đọc hồ sơ.
So sánh chuỗi JSON nguyên văn trên FE có thể tạo khác biệt giả do thứ tự key; dùng kết quả API.

### Migration và môi trường

Migration `20261008004706_ApplicationReviewVersions` tạo application_versions/application_comments,
FK ràng buộc comment thuộc đúng hồ sơ/version, unique `(application_id, version_number)` và index hàng chờ.
Hồ sơ SUBMITTED cũ (API cũ đã khóa sửa) được backfill version 1 từ dữ liệu hiện có;
DRAFT cũ được cấp mã, chưa tạo snapshot. Không tái dựng lịch sử không tồn tại.
APPROVED/CANCELLED đã là trạng thái hợp lệ trong schema, chưa có API duyệt/hủy trong TASK-11.

Connection key vẫn là `ConnectionStrings__WorkflowDatabase`. Cấu hình local hiện dùng database độc lập
`wardmate_workflow_db`; giữ tên đang dùng để không làm mất kết nối dữ liệu cũ.
Nếu môi trường mới dùng tên `wardmate_application_db` trong đặc tả, trỏ connection string đến database đó
và chạy migration của Workflow. Không trỏ vào database IAM/Procedure.
Chưa áp dụng migration trên Azure trong task này.

