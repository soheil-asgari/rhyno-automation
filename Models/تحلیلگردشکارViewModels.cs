namespace OfficeAutomation.Models;

public sealed class درخواستتحلیلگردشکار
{
    public DateTimeOffset? ازتاریخ { get; init; }
    public DateTimeOffset? تاتاریخ { get; init; }
    public int تعدادبیشینه { get; init; } = 10;
}

public sealed class دادهنمودار
{
    public string عنوان { get; init; } = string.Empty;
    public IReadOnlyList<string> برچسبها { get; init; } = [];
    public IReadOnlyList<سرینمودار> سریها { get; init; } = [];
}

public sealed class سرینمودار
{
    public string برچسب { get; init; } = string.Empty;
    public string رنگ { get; init; } = string.Empty;
    public IReadOnlyList<decimal> مقادیر { get; init; } = [];
}

public sealed class گزارشزمانچرخه
{
    public decimal میانگینساعت { get; init; }
    public decimal میانهساعت { get; init; }
    public decimal بیشینهساعت { get; init; }
    public int تعدادنمونه { get; init; }
    public IReadOnlyList<آیتمزمانچرخه> اقلام { get; init; } = [];
    public دادهنمودار نمودارروند { get; init; } = new();
}

public sealed class آیتمزمانچرخه
{
    public string نامفرآیند { get; init; } = string.Empty;
    public int تعداد { get; init; }
    public decimal میانگینساعت { get; init; }
    public decimal میانهساعت { get; init; }
    public decimal کمینهساعت { get; init; }
    public decimal بیشینهساعت { get; init; }
}

public sealed class گزارشگلوگاه
{
    public IReadOnlyList<آیتمگلوگاه> اقلام { get; init; } = [];
    public دادهنمودار نمودارگلوگاهها { get; init; } = new();
}

public sealed class آیتمگلوگاه
{
    public string نامفرآیند { get; init; } = string.Empty;
    public string ناممرحله { get; init; } = string.Empty;
    public int تعداد { get; init; }
    public decimal میانگینساعتتوقف { get; init; }
    public decimal بیشینهساعتتوقف { get; init; }
    public bool فعالاست { get; init; }
}

public sealed class گزارشماندگاریتایید
{
    public int تعدادکل { get; init; }
    public decimal میانگینساعتانتظار { get; init; }
    public IReadOnlyList<آیتمماندگاریتایید> اقلام { get; init; } = [];
    public دادهنمودار نمودارتوزیع { get; init; } = new();
}

public sealed class آیتمماندگاریتایید
{
    public string نامفرآیند { get; init; } = string.Empty;
    public int تعداددرانتظار { get; init; }
    public decimal میانگینساعتانتظار { get; init; }
    public decimal بیشینهساعتانتظار { get; init; }
}

public sealed class گزارشرویدادممیزی
{
    public int تعدادکل { get; init; }
    public int تعدادحساس { get; init; }
    public IReadOnlyList<آیتمرویدادممیزی> اقلام { get; init; } = [];
    public دادهنمودار نموداررویدادها { get; init; } = new();
}

public sealed class آیتمرویدادممیزی
{
    public string ماژول { get; init; } = string.Empty;
    public int تعداد { get; init; }
    public int تعدادحساس { get; init; }
}

public sealed class گزارشتحلیلیفرآیند
{
    public گزارشزمانچرخه زمانچرخه { get; init; } = new();
    public گزارشگلوگاه گلوگاهها { get; init; } = new();
    public گزارشماندگاریتایید ماندگاریتایید { get; init; } = new();
    public گزارشرویدادممیزی رویدادهایممیزی { get; init; } = new();
}
