using Paw.Core.Utils.DataStructures;
using System;
using System.Diagnostics;
using System.Linq;

namespace Paw.Core.Tests.Utils.DataStructures;

[TestFixture]
public class PackedGenPoolTests // TODO
{
    [Test]
    public void RoundTrip1()
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
    }

    [Test]
    public void AsSpan_HasSequentialDataAndPoolMustBeResized_ReturnsSpanToData()
    {
        // Arrange
        var testee = new PackedGenPool<int>(3);
        for (int i = 1; i <= 10; i++)
            testee.Alloc(i * 100);

        // Act
        int[] data = testee.AsSpan().ToArray();

        // Asset
        Assert.That(data, Has.Length.EqualTo(10));
        Assert.That(data, Is.EqualTo([100, 200, 300, 400, 500, 600, 700, 800, 900, 1000]));
    }

    [Test]
    public void GetIndexAndData_HasSequentialDenseData_ReturnsIndicesAndData()
    {
        // Arrange
        var testee = new PackedGenPool<int>();
        var ref1 = testee.Alloc(100);
        var ref2 = testee.Alloc(200);
        var ref3 = testee.Alloc(300);

        // Act
        var indicesAndData = testee.GetIndexAndData().ToArray();

        // Assert
        Assert.That(indicesAndData, Has.Length.EqualTo(3));
        Assert.That(indicesAndData, Is.EqualTo([
            (ref1, 100),
            (ref2, 200),
            (ref3, 300),
        ]));
    }

    [Test]
    public void GetIndexAndData_HasMovedDenseData_ReturnsIndicesAndData()
    {
        // Arrange
        var testee = new PackedGenPool<int>();
        var ref1 = testee.Alloc(100);
        var ref2 = testee.Alloc(200);
        var ref3 = testee.Alloc(300);
        testee.Free(ref2);

        // Act
        var indicesAndData = testee.GetIndexAndData().ToArray();

        // Assert
        Assert.That(indicesAndData, Has.Length.EqualTo(2));
        Assert.That(indicesAndData, Is.EqualTo([
            (ref1, 100),
            (ref3, 300),
        ]));
    }

    [Test]
    public void FreeWhere_PredicateMatchesSomeData_RemovesMatchedData()
    {
        // Arrange
        var testee = new PackedGenPool<int>();
        for (int i = 100; i <= 110; i++)
            _ = testee.Alloc(i);
        Assert.That(testee.Count, Is.EqualTo(11));

        // Act
        int removed = testee.FreeWhere(data => data % 2 == 0);
        // should remove: 100, 102, 104, 106, 108, 110
        // should keep: 101, 103, 105, 107, 109

        // Assert
        Assert.That(removed, Is.EqualTo(6));
        Assert.That(testee.Count, Is.EqualTo(5));
        Assert.That(testee.AsReadOnlySpan().ToArray(), Is.EquivalentTo([101, 103, 105, 107, 109])); // note: order can change
    }
}
