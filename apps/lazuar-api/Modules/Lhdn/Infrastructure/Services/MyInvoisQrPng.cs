using QRCoder;

namespace Modules.Lhdn.Infrastructure.Services;

internal static class MyInvoisQrPng
{
    public static byte[] Encode(string shareUrl)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(shareUrl, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(qrCodeData);
        return qrCode.GetGraphic(5);
    }
}
