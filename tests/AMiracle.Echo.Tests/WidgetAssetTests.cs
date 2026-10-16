using System.Net;

namespace AMiracle.Echo.Tests;

public class WidgetAssetTests
{
    [Fact]
    public async Task Serves_the_widget_with_both_screenshot_choices()
    {
        await using var host = await EchoTestHost.StartAsync();
        using var res = await host.Client.GetAsync("/echo/widget.js");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var js = await res.Content.ReadAsStringAsync();
        Assert.Contains("getDisplayMedia", js);   // capture this screen
        Assert.Contains("js-attach", js);         // pick an image file
        Assert.Contains("setLocale", js);
    }
}
