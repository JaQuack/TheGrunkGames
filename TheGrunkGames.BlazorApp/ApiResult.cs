namespace TheGrunkGames.BlazorApp
{
    public record ApiResult(bool Success, string? Error)
    {
        public static ApiResult Ok() => new(true, null);
        public static ApiResult Fail(string error) => new(false, error);
    }

    public record ApiResult<T>(bool Success, T? Data, string? Error)
    {
        public static ApiResult<T> Ok(T data) => new(true, data, null);
        public static ApiResult<T> Fail(string error) => new(false, default, error);
    }
}
