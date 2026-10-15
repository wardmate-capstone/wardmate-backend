using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using WardMate.Services.DocumentForm.Application.Interfaces;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace WardMate.Services.DocumentForm.Infrastructure.QrCode;

/// <summary>
/// Chèn hình ảnh QR code vào cuối tài liệu DOCX bằng OpenXML SDK.
/// Được gọi sau bước DocxCommentStripper và trước Gotenberg PDF render.
/// </summary>
public sealed class DocxQrStamper : IDocxQrStamper
{
    // 3.5 cm × 3.5 cm (1 cm = 360 000 EMU)
    private const long QrSizeEmu = 1_260_000L;

    public Stream StampQr(Stream docxStream, byte[] qrImagePng)
    {
        var output = new MemoryStream();
        docxStream.Position = 0;
        docxStream.CopyTo(output);
        output.Position = 0;

        using (var doc = WordprocessingDocument.Open(output, isEditable: true,
                   new OpenSettings { AutoSave = false }))
        {
            var mainPart = doc.MainDocumentPart
                ?? throw new InvalidOperationException("DOCX thiếu MainDocumentPart.");

            // Thêm PNG làm image part và lấy relationship id
            var imagePart = mainPart.AddImagePart(ImagePartType.Png);
            using (var imgStream = new MemoryStream(qrImagePng))
                imagePart.FeedData(imgStream);
            var relId = mainPart.GetIdOfPart(imagePart);

            // Tạo Drawing inline với ảnh QR
            var drawing = BuildInlineDrawing(relId, QrSizeEmu, QrSizeEmu);

            // Đoạn văn căn phải chứa QR — chèn trước SectionProperties (nếu có)
            var qrParagraph = new Paragraph(
                new ParagraphProperties(new Justification { Val = JustificationValues.Right }),
                new Run(drawing));

            var body = mainPart.Document.Body
                ?? throw new InvalidOperationException("DOCX thiếu Body.");

            var lastSection = body.Elements<SectionProperties>().LastOrDefault();
            if (lastSection is not null)
                body.InsertBefore(qrParagraph, lastSection);
            else
                body.AppendChild(qrParagraph);

            mainPart.Document.Save();
            doc.Save();
        }

        output.Position = 0;
        return output;
    }

    private static Drawing BuildInlineDrawing(string relId, long cx, long cy)
    {
        return new Drawing(
            new DW.Inline(
                new DW.Extent { Cx = cx, Cy = cy },
                new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new DW.DocProperties { Id = (UInt32Value)9001U, Name = "QR-MaTraCuu" },
                new DW.NonVisualGraphicFrameDrawingProperties(
                    new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = (UInt32Value)0U, Name = "QR.png" },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip { Embed = relId },
                                new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 0L, Y = 0L },
                                    new A.Extents { Cx = cx, Cy = cy }),
                                new A.PresetGeometry(new A.AdjustValueList())
                                    { Preset = A.ShapeTypeValues.Rectangle })))
                        { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
                {
                    DistanceFromTop = 0U,
                    DistanceFromBottom = 0U,
                    DistanceFromLeft = 0U,
                    DistanceFromRight = 0U
                }
        );
    }
}
