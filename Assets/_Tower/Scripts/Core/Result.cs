namespace Tower.Core
{
    public readonly struct Result
    {
        private Result(bool isSuccess, string error)
        {
            IsSuccess = isSuccess;
            Error = error;
        }

        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public string Error { get; }

        public static Result Success()
        {
            return new Result(true, string.Empty);
        }

        public static Result Failure(string error)
        {
            return new Result(false, error);
        }
    }

    public readonly struct Result<T>
    {
        private readonly T value;

        private Result(bool isSuccess, T value, string error)
        {
            IsSuccess = isSuccess;
            this.value = value;
            Error = error;
        }

        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public string Error { get; }
        public T Value => IsSuccess ? value : default;

        public static Result<T> Success(T value)
        {
            return new Result<T>(true, value, string.Empty);
        }

        public static Result<T> Failure(string error)
        {
            return new Result<T>(false, default, error);
        }
    }
}
