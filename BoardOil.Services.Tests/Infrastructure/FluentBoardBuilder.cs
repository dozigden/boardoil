using System.Numerics;
using BoardOil.Ef;
using BoardOil.Ef.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Services.Tests.Infrastructure;

public sealed class FluentBoardBuilder
{
    private const int KeyLength = 20;
    private const int BaseValue = 36;
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private static readonly BigInteger MaxValue = BigInteger.Pow(BaseValue, KeyLength) - 1;

    private readonly BoardOilDbContext _db;
    private readonly DateTime _nowUtc;
    private readonly Board _board;
    private readonly List<BoardColumn> _columns = [];
    private readonly Dictionary<string, BoardColumn> _columnsByTitle = new(StringComparer.Ordinal);
    private BoardColumn? _currentColumn;

    internal FluentBoardBuilder(BoardOilDbContext db, string boardName, DateTime nowUtc)
    {
        _db = db;
        _nowUtc = nowUtc;

        _board = new Board
        {
            Name = boardName,
            CreatedAtUtc = _nowUtc,
            UpdatedAtUtc = _nowUtc
        };

        _db.Boards.Add(_board);
        _db.SaveChanges();
    }

    public int BoardId => _board.Id;

    public FluentBoardBuilder AddColumn(string title)
    {
        var column = new BoardColumn
        {
            BoardId = _board.Id,
            Title = title,
            Position = _columns.Count,
            CreatedAtUtc = _nowUtc,
            UpdatedAtUtc = _nowUtc
        };

        _db.Columns.Add(column);
        _db.SaveChanges();

        _columns.Add(column);
        _columnsByTitle[title] = column;
        _currentColumn = column;

        return this;
    }

    public FluentBoardBuilder AddCard(string title, string description, int? position = null)
    {
        if (_currentColumn is null)
        {
            throw new InvalidOperationException("Cannot add a card before adding a column.");
        }

        var cards = _db.Cards
            .Where(x => x.BoardColumnId == _currentColumn.Id)
            .OrderBy(x => x.SortKey)
            .ToList();

        var insertIndex = position is null
            ? cards.Count
            : Math.Clamp(position.Value, 0, cards.Count);

        var previousKey = insertIndex > 0 ? cards[insertIndex - 1].SortKey : null;
        var nextKey = insertIndex < cards.Count ? cards[insertIndex].SortKey : null;
        var sortKey = GenerateBetween(previousKey, nextKey);

        var card = new BoardCard
        {
            BoardColumnId = _currentColumn.Id,
            Title = title,
            Description = description,
            SortKey = sortKey,
            CreatedAtUtc = _nowUtc,
            UpdatedAtUtc = _nowUtc
        };

        _db.Cards.Add(card);
        _db.SaveChanges();

        return this;
    }

    public BoardColumn GetColumn(string title)
    {
        if (_columnsByTitle.TryGetValue(title, out var column))
        {
            return column;
        }

        throw new InvalidOperationException($"Column '{title}' was not found in the fluent test builder.");
    }

    public BoardCard GetCard(string columnTitle, string cardTitle)
    {
        var column = GetColumn(columnTitle);
        var card = _db.Cards
            .Where(x => x.BoardColumnId == column.Id && x.Title == cardTitle)
            .OrderBy(x => x.SortKey)
            .FirstOrDefault();

        if (card is not null)
        {
            return card;
        }

        throw new InvalidOperationException(
            $"Card '{cardTitle}' was not found in column '{columnTitle}' in the fluent test builder.");
    }

    private static string GenerateBetween(string? previous, string? next)
    {
        var low = previous is null ? -1 : Parse(previous);
        var high = next is null ? MaxValue + 1 : Parse(next);
        if (high <= low + 1)
        {
            throw new InvalidOperationException("Unable to allocate a sort key between neighbors.");
        }

        return Format((low + high) / 2);
    }

    private static BigInteger Parse(string key)
    {
        if (key.Length != KeyLength)
        {
            throw new ArgumentException($"Sort key must be exactly {KeyLength} characters.", nameof(key));
        }

        BigInteger value = 0;
        foreach (var raw in key)
        {
            var c = char.ToUpperInvariant(raw);
            var digit = Alphabet.IndexOf(c);
            if (digit < 0)
            {
                throw new ArgumentException("Sort key contains invalid characters.", nameof(key));
            }

            value = (value * BaseValue) + digit;
        }

        return value;
    }

    private static string Format(BigInteger value)
    {
        if (value < 0 || value > MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        var chars = new char[KeyLength];
        var remainder = value;
        for (var i = KeyLength - 1; i >= 0; i--)
        {
            remainder = BigInteger.DivRem(remainder, BaseValue, out var digit);
            chars[i] = Alphabet[(int)digit];
        }

        return new string(chars);
    }
}
