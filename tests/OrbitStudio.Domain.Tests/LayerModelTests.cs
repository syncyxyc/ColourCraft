using OrbitStudio.Domain.Imaging;
using OrbitStudio.Domain.Layers;

namespace OrbitStudio.Domain.Tests;

public class LayerModelTests
{
    private static PixelLayer Layer(string name = "L") => new(name, PixelBuffer.Create(2, 2));

    [Fact]
    public void Group_InsertAndRemove_MaintainParent()
    {
        var group = new LayerGroup("G");
        var a = Layer("A");
        var b = Layer("B");

        group.Add(a);
        group.Insert(0, b);

        group.Children.ShouldBe([b, a]);
        a.Parent.ShouldBeSameAs(group);
        group.Remove(a);
        a.Parent.ShouldBeNull();
        group.Children.ShouldBe([b]);
    }

    [Fact]
    public void Insert_NodeWithParent_Throws()
    {
        var g1 = new LayerGroup("1");
        var g2 = new LayerGroup("2");
        var a = Layer();
        g1.Add(a);

        Should.Throw<InvalidOperationException>(() => g2.Add(a));
    }

    [Fact]
    public void Insert_Cycle_Throws()
    {
        var outer = new LayerGroup("außen");
        var inner = new LayerGroup("innen");
        outer.Add(inner);

        // Gruppe in sich selbst
        var lone = new LayerGroup("allein");
        Should.Throw<InvalidOperationException>(() => lone.Add(lone));

        // „außen" (ohne Elterngruppe) in sein eigenes Kind „innen" einfügen wäre ein Zyklus
        Should.Throw<InvalidOperationException>(() => inner.Add(outer));
        outer.Children.ShouldBe([inner]);
        inner.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Move_ReordersWithinGroup()
    {
        var group = new LayerGroup("G");
        var a = Layer("A");
        var b = Layer("B");
        var c = Layer("C");
        group.Add(a);
        group.Add(b);
        group.Add(c);

        group.Move(a, 2);

        group.Children.ShouldBe([b, c, a]);
        Should.Throw<ArgumentOutOfRangeException>(() => group.Move(a, 3));
    }

    [Fact]
    public void Descendants_AreInPaintOrder()
    {
        var root = new LayerGroup("root");
        var bottom = Layer("unten");
        var group = new LayerGroup("G");
        var inner = Layer("innen");
        var top = Layer("oben");
        root.Add(bottom);
        root.Add(group);
        group.Add(inner);
        root.Add(top);

        root.Descendants().ShouldBe([bottom, group, inner, top]);
        root.PixelLayers().ShouldBe([bottom, inner, top]);
        inner.Depth.ShouldBe(1);
        top.Depth.ShouldBe(0);
    }

    [Fact]
    public void EffectiveVisibilityAndLock_FollowParents()
    {
        var root = new LayerGroup("root");
        var group = new LayerGroup("G");
        var layer = Layer();
        root.Add(group);
        group.Add(layer);

        group.IsVisible = false;
        group.IsLocked = true;

        layer.IsVisible.ShouldBeTrue();
        layer.IsEffectivelyVisible.ShouldBeFalse();
        layer.IsEffectivelyLocked.ShouldBeTrue();
    }

    [Theory]
    [InlineData(-0.5, 0.0)]
    [InlineData(0.25, 0.25)]
    [InlineData(7.0, 1.0)]
    public void Opacity_IsClamped(double input, double expected)
    {
        var layer = Layer();
        layer.Opacity = input;
        layer.Opacity.ShouldBe(expected);
    }

    [Fact]
    public void Opacity_RejectsNaN_AndNameRejectsEmpty()
    {
        var layer = Layer();
        Should.Throw<ArgumentOutOfRangeException>(() => layer.Opacity = double.NaN);
        Should.Throw<ArgumentException>(() => layer.Name = "  ");
    }

    [Fact]
    public void Duplicate_IsDeepAndIndependent()
    {
        var group = new LayerGroup("G") { Opacity = 0.5, BlendMode = BlendMode.Screen, IsLocked = true };
        var layer = Layer("A");
        layer.Pixels.Fill(Bgra32.White);
        layer.Mask = LayerMask.CreateRevealAll(2, 2);
        group.Add(layer);

        var copy = (LayerGroup)group.Duplicate("G Kopie");
        var copiedLayer = (PixelLayer)copy.Children[0];
        copiedLayer.Pixels.SetPixel(0, 0, Bgra32.Black);
        copiedLayer.Mask!.Set(0, 0, 0);

        copy.Id.ShouldNotBe(group.Id);
        copy.Opacity.ShouldBe(0.5);
        copy.BlendMode.ShouldBe(BlendMode.Screen);
        copy.IsLocked.ShouldBeFalse(); // Kopien sind nie gesperrt
        layer.Pixels.GetPixel(0, 0).ShouldBe(Bgra32.White);
        layer.Mask.Get(0, 0).ShouldBe((byte)255);
        copiedLayer.Parent.ShouldBeSameAs(copy);
    }

    [Fact]
    public void Mask_RevealAll_Invert_Fill()
    {
        var mask = LayerMask.CreateRevealAll(3, 2);
        mask.Get(2, 1).ShouldBe((byte)255);

        mask.Fill(new IntRect(0, 0, 1, 2), 100);
        mask.Invert();

        mask.Get(0, 0).ShouldBe((byte)155);
        mask.Get(2, 1).ShouldBe((byte)0);
        mask.GetRow(1).Length.ShouldBe(3);
    }
}
