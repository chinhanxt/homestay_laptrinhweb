namespace WebHomestay.Services.AI.Workflow;

public static class SemanticQueryHelper
{
    public static bool IsSemanticQuery(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        var msg = message.ToLowerInvariant();
        var keywords = new[]
        {
            "lãng mạn", "lang man", "bồn tắm", "bon tam", "yên tĩnh", "yen tinh",
            "ban công", "ban cong", "view", "phòng rộng", "phòng đẹp", "phong rong", "phong dep",
            "giá rẻ", "gia re", "yên bình", "yen binh", "tiện nghi", "tien nghi",
            "nước nóng", "nuoc nong", "tự do", "tu do", "riêng tư", "rieng tu",
            "thoải mái", "thoai mai", "decor", "check-in", "sống ảo", "song ao",
            "chụp hình", "chup hinh", "cây xanh", "cay xanh", "thoáng", "thoang",
            "chữa lành", "chua lanh", "chill", "hẹn hò", "hen ho", "cặp đôi", "cap doi",
            "gia đình", "gia dinh", "nhóm", "nhom", "bàn làm việc", "ban lam viec", "cửa sổ", "cua so",
            "tìm phòng", "tim phong", "tư vấn", "tu van", "phòng nào", "phong nao", "có gì", "co gi"
        };
        return keywords.Any(k => msg.Contains(k));
    }
}
