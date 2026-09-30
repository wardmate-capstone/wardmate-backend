namespace WardMate.Services.DocumentForm.Domain.Models;

/// <summary>
/// Các kiểu dữ liệu đầu vào được hỗ trợ trên biểu mẫu động E-Form.
/// </summary>
public enum FormFieldType
{
    Text = 1,
    Number = 2,
    Date = 3,
    DateTime = 4,
    Select = 5,
    Radio = 6,
    Checkbox = 7,
    Textarea = 8,
    NationalId = 9,      // Căn cước công dân / Định danh
    PhoneNumber = 10,    // Số điện thoại Việt Nam
    Email = 11,          // Địa chỉ email
    Currency = 12        // Tiền tệ VNĐ
}
