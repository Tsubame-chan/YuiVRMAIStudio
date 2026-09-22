using NUnit.Framework;
using UnityEngine;
using YuiPhysicalAI.UI;

public class YuiPopupPlacementTests
{
    [Test]
    public void MenuFollowsComposerAndStaysWithinBothWindowOrientations()
    {
        foreach (var size in new[] { new Vector2(1080,1920), new Vector2(1920,1080),new Vector2(340,500) })
        foreach (var anchor in new[] {new Rect(-130,-200,80,80),new Rect(size.x/2-70,size.y/2-80,60,60)})
        {
            var bounds=new Rect(-size/2,size);
            var popup=YuiPopupPlacement.Above(bounds,anchor,new Vector2(450,284));
            Assert.That(popup.xMin,Is.GreaterThanOrEqualTo(bounds.xMin+16));
            Assert.That(popup.xMax,Is.LessThanOrEqualTo(bounds.xMax-16));
            Assert.That(popup.yMin,Is.GreaterThanOrEqualTo(bounds.yMin+16));
            Assert.That(popup.yMax,Is.LessThanOrEqualTo(bounds.yMax-16));
        }
    }
    [Test]
    public void SufficientSpacePlacesMenuImmediatelyAboveItsButton()
    {
        var anchor=new Rect(20,100,80,112);
        var popup=YuiPopupPlacement.Above(new Rect(0,0,1080,1920),anchor,new Vector2(450,284));
        Assert.That(popup.xMin,Is.EqualTo(anchor.xMin));
        Assert.That(popup.yMin,Is.EqualTo(anchor.yMax+12));
    }
}
