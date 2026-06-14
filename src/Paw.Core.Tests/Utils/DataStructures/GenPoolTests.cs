using Paw.Core.Utils.DataStructures;
using System;

namespace Paw.Core.Tests.Utils.DataStructures;

[TestFixture]
internal class GenPoolTests
{
    [Test]
    public void Constructor_InitialSizeLessThanOne_ThrowsArgumentOutOfRangeException()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => _ = new GenPool<int>(0))!;

        Assert.That(exception.ParamName, Is.EqualTo("initialSize"));
        Assert.That(exception.Message, Does.Contain("Must be 1 or greater"));
    }

    [Test]
    public void Constructor_CreatingPools_AssignsUniquePositivePoolIds()
    {
        GenPool<int> firstPool = new();
        GenPool<int> secondPool = new();

        Assert.That(firstPool.PoolId, Is.GreaterThan(0));
        Assert.That(secondPool.PoolId, Is.GreaterThan(0));
        Assert.That(secondPool.PoolId, Is.Not.EqualTo(firstPool.PoolId));
    }

    [Test]
    public void IsValid_AllocatedReference_ReturnsTrue()
    {
        GenPool<int> pool = new();
        GenIndex reference = pool.Alloc(123);

        bool isValid = pool.IsValid(reference);

        Assert.That(isValid, Is.True);
    }

    [Test]
    public void IsValid_IndexIsNegative_ReturnsFalse()
    {
        GenPool<int> pool = new(1);
        GenIndex reference = new(pool.PoolId, -1, 0);

        bool isValid = pool.IsValid(reference);

        Assert.That(isValid, Is.False);
    }

    [Test]
    public void IsValid_IndexIsOutsidePoolBounds_ReturnsFalse()
    {
        GenPool<int> pool = new(1);
        GenIndex reference = new(pool.PoolId, 1, 0);

        bool isValid = pool.IsValid(reference);

        Assert.That(isValid, Is.False);
    }

    [Test]
    public void IsValid_SlotIsUnused_ReturnsFalse()
    {
        GenPool<int> pool = new(1);
        GenIndex reference = new(pool.PoolId, 0, 0);

        bool isValid = pool.IsValid(reference);

        Assert.That(isValid, Is.False);
    }

    [Test]
    public void IsValid_GenerationDoesNotMatch_ReturnsFalse()
    {
        GenPool<int> pool = new();
        GenIndex allocatedReference = pool.Alloc(123);
        GenIndex reference = new(pool.PoolId, allocatedReference.Index, allocatedReference.Gen + 1);

        bool isValid = pool.IsValid(reference);

        Assert.That(isValid, Is.False);
    }

    [Test]
    public void IsValid_PoolIdDoesNotMatch_ReturnsFalse()
    {
        GenPool<int> pool = new();
        GenPool<int> otherPool = new();
        GenIndex reference = otherPool.Alloc(123);

        bool isValid = pool.IsValid(reference);

        Assert.That(isValid, Is.False);
    }

    [Test]
    public void Get_ReferenceIsValid_ReturnsStoredValue()
    {
        GenPool<int> pool = new();
        GenIndex reference = pool.Alloc(123);

        int value = pool.Get(reference);

        Assert.That(value, Is.EqualTo(123));
    }

    [Test]
    public void Get_ReferenceIsInvalid_ThrowsArgumentException()
    {
        GenPool<int> pool = new();
        GenIndex reference = pool.Alloc(123);
        pool.Free(reference);

        ArgumentException exception = Assert.Throws<ArgumentException>(() => _ = pool.Get(reference))!;

        Assert.That(exception.Message, Does.Contain("Get: invalid ref"));
    }

    [Test]
    public void GetRef_ReferenceIsValid_ReturnsReferenceToStoredValue()
    {
        GenPool<int> pool = new();
        GenIndex reference = pool.Alloc(123);

        ref int valueReference = ref pool.GetRef(reference);
        valueReference = 456;

        Assert.That(pool.Get(reference), Is.EqualTo(456));
    }

    [Test]
    public void GetRef_ReferenceIsInvalid_ThrowsArgumentException()
    {
        GenPool<int> pool = new();
        GenIndex reference = pool.Alloc(123);
        pool.Free(reference);

        ArgumentException exception = Assert.Throws<ArgumentException>(() => pool.GetRef(reference))!;

        Assert.That(exception.Message, Does.Contain("GetRef: invalid ref"));
    }

    [Test]
    public void Set_ReferenceIsValid_UpdatesStoredValue()
    {
        GenPool<int> pool = new();
        GenIndex reference = pool.Alloc(123);

        pool.Set(reference, 456);

        Assert.That(pool.Get(reference), Is.EqualTo(456));
    }

    [Test]
    public void Set_ReferenceIsInvalid_ThrowsArgumentException()
    {
        GenPool<int> pool = new();
        GenIndex reference = pool.Alloc(123);
        pool.Free(reference);

        ArgumentException exception = Assert.Throws<ArgumentException>(() => pool.Set(reference, 456))!;

        Assert.That(exception.Message, Does.Contain("Set: invalid ref"));
    }

    [Test]
    public void Borrow_ActionIsNull_ThrowsArgumentNullException()
    {
        GenPool<int> pool = new();
        GenIndex reference = pool.Alloc(123);

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => pool.Borrow(reference, null!))!;

        Assert.That(exception.ParamName, Is.EqualTo("action"));
    }

    [Test]
    public void Borrow_ReferenceIsInvalid_ThrowsArgumentException()
    {
        GenPool<int> pool = new();
        GenIndex reference = pool.Alloc(123);
        pool.Free(reference);

        ArgumentException exception = Assert.Throws<ArgumentException>(() => pool.Borrow(reference, static (ref int data) => data = 456))!;

        Assert.That(exception.Message, Does.Contain("Borrow: invalid ref"));
    }

    [Test]
    public void Borrow_ReferenceIsValid_AllowsMutationOfStoredValue()
    {
        GenPool<int> pool = new();
        GenIndex reference = pool.Alloc(123);

        pool.Borrow(reference, static (ref int data) => data = 456);

        Assert.That(pool.Get(reference), Is.EqualTo(456));
    }

    [Test]
    public void Borrow_ActionThrows_RethrowsAndAllowsLaterModifications()
    {
        GenPool<int> pool = new();
        GenIndex reference = pool.Alloc(123);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => pool.Borrow(reference, static (ref int data) =>
        {
            data = 456;
            throw new InvalidOperationException("boom");
        }))!;

        Assert.That(exception.Message, Is.EqualTo("boom"));
        Assert.That(pool.Get(reference), Is.EqualTo(456));
        Assert.DoesNotThrow(() => pool.Free(reference));
    }

    [Test]
    public void Alloc_DataIsProvided_ReturnsValidReferenceWithStoredValue()
    {
        GenPool<int> pool = new();

        GenIndex reference = pool.Alloc(123);

        Assert.Multiple(() =>
        {
            Assert.That(reference.PoolId, Is.EqualTo(pool.PoolId));
            Assert.That(reference.Index, Is.EqualTo(0));
            Assert.That(reference.Gen, Is.EqualTo(1));
            Assert.That(pool.IsValid(reference), Is.True);
            Assert.That(pool.Get(reference), Is.EqualTo(123));
        });
    }

    [Test]
    public void Alloc_NoFreeSlots_ExpandsPoolAndReturnsNextIndex()
    {
        GenPool<int> pool = new(1);
        _ = pool.Alloc(123);

        GenIndex reference = pool.Alloc(456);

        Assert.Multiple(() =>
        {
            Assert.That(reference.Index, Is.EqualTo(1));
            Assert.That(pool.IsValid(reference), Is.True);
            Assert.That(pool.Get(reference), Is.EqualTo(456));
        });
    }

    [Test]
    public void Alloc_SlotWasFreed_ReusesIndexWithNewGeneration()
    {
        GenPool<int> pool = new(1);
        GenIndex firstReference = pool.Alloc(123);
        pool.Free(firstReference);

        GenIndex secondReference = pool.Alloc(456);

        Assert.Multiple(() =>
        {
            Assert.That(secondReference.Index, Is.EqualTo(firstReference.Index));
            Assert.That(secondReference.Gen, Is.EqualTo(firstReference.Gen + 1));
            Assert.That(pool.IsValid(firstReference), Is.False);
            Assert.That(pool.Get(secondReference), Is.EqualTo(456));
        });
    }

    [Test]
    public void Alloc_ReferenceIsBorrowed_ThrowsInvalidOperationException()
    {
        GenPool<int> pool = new();
        GenIndex reference = pool.Alloc(123);
        InvalidOperationException? exception = null;

        pool.Borrow(reference, (ref int data) =>
        {
            exception = Assert.Throws<InvalidOperationException>(() => _ = pool.Alloc(456));
        });

        Assert.That(exception?.Message, Does.Contain("GenPool cannot be modified while references are borrowed"));
    }

    [Test]
    public void Free_ReferenceIsValid_InvalidatesReference()
    {
        GenPool<int> pool = new();
        GenIndex reference = pool.Alloc(123);

        pool.Free(reference);

        Assert.That(pool.IsValid(reference), Is.False);
    }

    [Test]
    public void Free_ReferenceIsInvalid_ThrowsArgumentException()
    {
        GenPool<int> pool = new();
        GenIndex reference = pool.Alloc(123);
        pool.Free(reference);

        ArgumentException exception = Assert.Throws<ArgumentException>(() => pool.Free(reference))!;

        Assert.That(exception.Message, Does.Contain("Free: invalid ref"));
    }

    [Test]
    public void Free_ReferenceIsBorrowed_ThrowsInvalidOperationException()
    {
        GenPool<int> pool = new();
        GenIndex reference = pool.Alloc(123);
        InvalidOperationException? exception = null;

        pool.Borrow(reference, (ref int data) =>
        {
            exception = Assert.Throws<InvalidOperationException>(() => pool.Free(reference));
        });

        Assert.That(exception?.Message, Does.Contain("GenPool cannot be modified while references are borrowed"));
    }
}
