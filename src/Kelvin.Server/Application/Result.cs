namespace Kelvin.Server.Application;

public record Error(string Code, string Message)
{
  public static readonly Error None = new(string.Empty, string.Empty);
  public static readonly Error NullValue = new("Error.NullValue", "The specified result value is null.");
}

public class Result
{
  public bool IsSuccess { get; }
  public bool IsFailure => !IsSuccess;

  public Error Error { get; }

  protected Result(bool isSuccess, Error error)
  {
    if (isSuccess && error != Error.None)
      throw new InvalidOperationException();

    if (!isSuccess && error == Error.None)
      throw new InvalidOperationException();

    IsSuccess = isSuccess;
    Error = error;
  }

  public static Result Success() => new(true, Error.None);

  public static Result Failure(Error error) => new(false, error);

  public void EnsureSuccess()
  {
    if (IsFailure)
      throw new InvalidOperationException($"Operation failed with error: {Error.Code} - {Error.Message}");
  }
}

public class Result<T> : Result
{
  public T? Value { get; }

  protected Result(T? value, bool isSuccess, Error error)
    : base(isSuccess, error)
  {
    Value = value;
  }

  public static Result<T> Success(T value) => new(value, true, Error.None);

  public static new Result<T> Failure(Error error) => new(default, false, error);
}

public class PagedResult<T> : Result
{
  public int? Page { get; }

  public int? PageSize { get; }

  public int? TotalCount { get; }

  public IEnumerable<T>? Items { get; }

  public bool HasNextPage => Page.HasValue && PageSize.HasValue && TotalCount.HasValue ? Page.Value * PageSize.Value < TotalCount.Value : false;

  public int TotalPages => PageSize > 0 && TotalCount is not null ? (int)Math.Ceiling(TotalCount.Value / (double)PageSize.Value) : 0;

  protected PagedResult(IEnumerable<T>? items, int? page, int? pageSize, int? totalCount, bool isSuccess, Error error)
    : base(isSuccess, error)
  {
    Items = items;
    Page = page;
    PageSize = pageSize;
    TotalCount = totalCount;
  }

  public static PagedResult<T> Success(IEnumerable<T> items, int page, int pageSize, int totalCount) =>
    new(items, page, pageSize, totalCount, true, Error.None);

  public static new PagedResult<T> Failure(Error error) => new(null, null, null, null, false, error);
}
