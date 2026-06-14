using Paw.Core.Utils.DataStructures;
using System;
using System.Diagnostics;

namespace Paw.Core.Tests.Utils.DataStructures;

[TestFixture]
public class PackedGenPoolTests // TODO
{
    [Test]
    public void Test1()
    {
        var testee = new PackedGenPool<int>(5);

        var ref1 = testee.Alloc(100);
        var ref2 = testee.Alloc(200);
        var ref3 = testee.Alloc(300);

        Assert.That(testee.Get(ref1), Is.EqualTo(100));
        Assert.That(testee.Get(ref2), Is.EqualTo(200));
        Assert.That(testee.Get(ref3), Is.EqualTo(300));

        testee.Set(ref2, 201);

        Assert.That(testee.Get(ref1), Is.EqualTo(100));
        Assert.That(testee.Get(ref2), Is.EqualTo(201));
        Assert.That(testee.Get(ref3), Is.EqualTo(300));

        testee.Free(ref2);

        Assert.That(testee.Get(ref1), Is.EqualTo(100));
        Assert.That(() => testee.Get(ref2), Throws.ArgumentException);
        Assert.That(testee.Get(ref3), Is.EqualTo(300));

        var ref4 = testee.Alloc(400);

        Assert.That(testee.Get(ref1), Is.EqualTo(100));
        Assert.That(() => testee.Get(ref2), Throws.ArgumentException);
        Assert.That(testee.Get(ref3), Is.EqualTo(300));
        Assert.That(testee.Get(ref4), Is.EqualTo(400));

        Debugger.Break();
    }

    [Test]
    public void Test2()
    {
        var testee = new PackedGenPool<int>(3);

        for (int i = 0; i < 20; i++)
        {
            testee.Alloc(i * 100);
        }

        int[] data = testee.AsSpan().ToArray();

        Console.WriteLine("...");
    }
}
