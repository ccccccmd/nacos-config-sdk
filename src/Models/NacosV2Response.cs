namespace Nacos.V2.Config.Models;

/// <summary>
/// Nacos v2 API unified response model
/// </summary>
/// <typeparam name="T">Data type</typeparam>
public class NacosV2Response<T>
{
    /// <summary>
    /// Response code (0 = success)
    /// </summary>
    public int Code { get; set; }

    /// <summary>
    /// Response message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Response data
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// Check if the response is successful
    /// </summary>
    public bool IsSuccess => Code == 0;
}
