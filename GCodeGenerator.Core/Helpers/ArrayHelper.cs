using System;

namespace GCodeGenerator.Core.Helpers;

/// <summary>
/// Вспомогательный класс для работы с массивами и индексами
/// </summary>
public static class ArrayHelper
{
    /// <summary>
    /// Индекс по умолчанию
    /// </summary>
    public const int DefaultIndex = 0;

    /// <summary>
    /// Проверяет, является ли индекс валидным для массива указанной длины
    /// </summary>
    /// <param name="index">Индекс для проверки</param>
    /// <param name="arrayLength">Длина массива</param>
    /// <returns>true, если индекс валиден; иначе false</returns>
    public static bool IsValidIndex(int index, int arrayLength)
    {
        return index >= 0 && index < arrayLength;
    }

    /// <summary>
    /// Находит валидный индекс для указанного значения в массиве
    /// </summary>
    /// <typeparam name="T">Тип элементов массива (должен быть enum)</typeparam>
    /// <param name="array">Массив для поиска</param>
    /// <param name="value">Значение для поиска</param>
    /// <returns>Валидный индекс или DefaultIndex, если значение не найдено или массив пуст</returns>
    public static int FindValidIndex<T>(T[] array, T value) where T : struct, Enum
    {
        if (array.Length == 0)
            return DefaultIndex;

        var index = Array.IndexOf(array, value);
        return IsValidIndex(index, array.Length) ? index : DefaultIndex;
    }

    /// <summary>
    /// Проверяет, что все массивы не пустые и все индексы валидны
    /// </summary>
    /// <param name="indices">Массив кортежей (индекс, длина массива) для проверки</param>
    /// <returns>true, если все массивы не пустые и все индексы валидны; иначе false</returns>
    public static bool AreIndicesValid(params (int index, int length)[] indices)
    {
        if (indices == null || indices.Length == 0)
            return false;

        foreach (var (index, length) in indices)
        {
            if (length == 0 || !IsValidIndex(index, length))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Нормализует индекс, устанавливая значение по умолчанию при выходе за границы
    /// </summary>
    /// <param name="index">Индекс для нормализации (передается по ссылке)</param>
    /// <param name="arrayLength">Длина массива</param>
    public static void NormalizeIndex(ref int index, int arrayLength)
    {
        if (!IsValidIndex(index, arrayLength))
            index = DefaultIndex;
    }

    /// <summary>
    /// Нормализует несколько индексов, устанавливая значения по умолчанию при выходе за границы
    /// </summary>
    /// <param name="indices">Массив индексов для нормализации (изменяется на месте)</param>
    /// <param name="arrayLengths">Массив длин массивов, соответствующих индексам</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, если indices или arrayLengths равны null</exception>
    /// <exception cref="ArgumentException">Выбрасывается, если длины массивов не совпадают</exception>
    public static void NormalizeIndices(int[] indices, int[] arrayLengths)
    {
        if (indices == null)
            throw new ArgumentNullException(nameof(indices));
        if (arrayLengths == null)
            throw new ArgumentNullException(nameof(arrayLengths));
        if (indices.Length != arrayLengths.Length)
            throw new ArgumentException("Количество индексов должно совпадать с количеством длин массивов");

        for (int i = 0; i < indices.Length; i++)
        {
            if (!IsValidIndex(indices[i], arrayLengths[i]))
                indices[i] = DefaultIndex;
        }
    }

    /// <summary>
    /// Нормализует два индекса, устанавливая значения по умолчанию при выходе за границы
    /// </summary>
    /// <param name="index1">Первый индекс для нормализации (передается по ссылке)</param>
    /// <param name="length1">Длина первого массива</param>
    /// <param name="index2">Второй индекс для нормализации (передается по ссылке)</param>
    /// <param name="length2">Длина второго массива</param>
    public static void NormalizeIndices(ref int index1, int length1, ref int index2, int length2)
    {
        NormalizeIndex(ref index1, length1);
        NormalizeIndex(ref index2, length2);
    }

    /// <summary>
    /// Нормализует три индекса, устанавливая значения по умолчанию при выходе за границы
    /// </summary>
    /// <param name="index1">Первый индекс для нормализации (передается по ссылке)</param>
    /// <param name="length1">Длина первого массива</param>
    /// <param name="index2">Второй индекс для нормализации (передается по ссылке)</param>
    /// <param name="length2">Длина второго массива</param>
    /// <param name="index3">Третий индекс для нормализации (передается по ссылке)</param>
    /// <param name="length3">Длина третьего массива</param>
    public static void NormalizeIndices(ref int index1, int length1, ref int index2, int length2, ref int index3, int length3)
    {
        NormalizeIndex(ref index1, length1);
        NormalizeIndex(ref index2, length2);
        NormalizeIndex(ref index3, length3);
    }
}

