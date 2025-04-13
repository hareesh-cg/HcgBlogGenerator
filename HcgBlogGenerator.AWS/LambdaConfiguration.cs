namespace HcgBlogGenerator.Aws;

public class LambdaConfiguration {
    // Environment variables will be mapped to these properties (e.g., SOURCE_BUCKET -> SourceBucket)
    public string? SOURCE_BUCKET {
        get; set;
    }
    public string? SOURCE_PREFIX {
        get; set;
    } // Optional: Prefix for source files within bucket (e.g., "src/")
    public string? OUTPUT_BUCKET {
        get; set;
    }
    public string? OUTPUT_PREFIX {
        get; set;
    } // Optional: Prefix for output files within bucket (e.g., "public/")
    public string? CONFIG_FILE_KEY {
        get; set;
    } // Optional: S3 key for config file relative to SOURCE_PREFIX (defaults to config.json)
}
