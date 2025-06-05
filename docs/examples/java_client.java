import okhttp3.*;
import com.google.gson.Gson;
import com.google.gson.JsonObject;
import java.io.File;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Paths;
import java.util.concurrent.TimeUnit;

/**
 * OWhisper.NET Java客户端示例
 * 演示如何使用Java调用OWhisper.NET API进行音频转写
 */
public class OWhisperClient {
    private final OkHttpClient client;
    private final String baseUrl;
    private final Gson gson;

    public OWhisperClient(String baseUrl) {
        // 支持环境变量配置
        if (baseUrl == null) {
            String host = System.getenv().getOrDefault("OWHISPER_HOST", "localhost");
            String port = System.getenv().getOrDefault("OWHISPER_PORT", "11899");
            baseUrl = String.format("http://%s:%s", host, port);
        }
        
        this.baseUrl = baseUrl;
        this.client = new OkHttpClient.Builder()
                .connectTimeout(30, TimeUnit.SECONDS)
                .writeTimeout(30, TimeUnit.MINUTES)
                .readTimeout(30, TimeUnit.MINUTES)
                .build();
        this.gson = new Gson();
    }

    public JsonObject getStatus() throws IOException {
        Request request = new Request.Builder()
                .url(baseUrl + "/api/status")
                .build();

        try (Response response = client.newCall(request).execute()) {
            String responseBody = response.body().string();
            return gson.fromJson(responseBody, JsonObject.class);
        }
    }

    public JsonObject transcribeFile(String filePath) throws IOException {
        File file = new File(filePath);
        
        // 确定Content-Type
        String contentType = "application/octet-stream";
        String fileName = file.getName().toLowerCase();
        if (fileName.endsWith(".mp3")) {
            contentType = "audio/mpeg";
        } else if (fileName.endsWith(".wav")) {
            contentType = "audio/wav";
        } else if (fileName.endsWith(".aac")) {
            contentType = "audio/aac";
        } else if (fileName.endsWith(".m4a")) {
            contentType = "audio/aac";
        }

        RequestBody fileBody = RequestBody.create(
            MediaType.parse(contentType), 
            file
        );

        RequestBody requestBody = new MultipartBody.Builder()
                .setType(MultipartBody.FORM)
                .addFormDataPart("file", file.getName(), fileBody)
                .build();

        Request request = new Request.Builder()
                .url(baseUrl + "/api/transcribe")
                .post(requestBody)
                .build();

        try (Response response = client.newCall(request).execute()) {
            String responseBody = response.body().string();
            return gson.fromJson(responseBody, JsonObject.class);
        }
    }

    // 使用示例
    public static void main(String[] args) {
        OWhisperClient client = new OWhisperClient(null);

        try {
            // 检查服务状态
            JsonObject status = client.getStatus();
            System.out.println("服务状态: " + status);

            // 转写音频文件
            JsonObject result = client.transcribeFile("audio.mp3");
            
            if ("success".equals(result.get("Status").getAsString())) {
                JsonObject data = result.getAsJsonObject("Data");
                String text = data.get("Text").getAsString();
                String srtContent = data.get("SrtContent").getAsString();
                double processingTime = data.get("ProcessingTime").getAsDouble();

                // 保存结果
                Files.write(Paths.get("output.txt"), text.getBytes("UTF-8"));
                Files.write(Paths.get("output.srt"), srtContent.getBytes("UTF-8"));

                System.out.printf("转写完成，耗时: %.1f秒%n", processingTime);
            } else {
                System.out.println("转写失败: " + result.get("Error").getAsString());
            }
        } catch (Exception e) {
            System.out.println("错误: " + e.getMessage());
        }
    }
} 