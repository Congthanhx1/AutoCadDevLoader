# AutoCadDevLoader

[English](README.md) | [Tiếng Việt](README.vi.md)

AutoCadDevLoader là công cụ hỗ trợ phát triển plugin AutoCAD .NET với giao diện gọn ngay trong AutoCAD. Công cụ rút ngắn vòng lặp sửa code–build–kiểm thử bằng cách nạp một bản sao tạm của DLL, tự dò lệnh và cho phép tìm, reload, chạy lệnh nhanh mà không phải liên tục khởi động lại AutoCAD.

Công cụ được thiết kế cho quá trình phát triển và gỡ lỗi plugin, không thay thế AutoCAD Application Bundle hoặc quy trình phát hành plugin chính thức.

## Demo

https://github.com/user-attachments/assets/6f62afa9-705f-427a-b9c4-42de544da431

> Video trên minh họa cách nạp **CadDevLoader** vào AutoCAD và nạp DLL plugin để phát triển trực tiếp.

## Điểm nổi bật

- **Reload mà không khóa DLL nguồn** — DLL build được sao chép vào một thư mục riêng trong `%TEMP%\CadDevLoader`, sau đó AutoCAD nạp bản sao này. Trình biên dịch vẫn có thể ghi đè DLL gốc ở lần build tiếp theo.
- **Tự động nhận diện lệnh** — quét assembly đã nạp để tìm các phương thức không có tham số và được đánh dấu bằng thuộc tính `CommandMethod` của AutoCAD.
- **Bảng chạy lệnh nhanh** — tìm kiếm và chạy lệnh đã nhận diện từ một panel nhỏ gọn trong AutoCAD.
- **Yêu thích và gần đây** — ghim lệnh thường dùng bằng nút ngôi sao và mở lại nhanh các lệnh vừa chạy.
- **Theo dõi bản build** — theo dõi DLL đang chọn và thông báo khi có bản build mới sẵn sàng để reload.
- **Reload một chạm** — Reload là thao tác chính; các thao tác ít dùng hơn được đặt trong menu `⋯`.
- **Thông báo reload rõ ràng** — hiển thị trạng thái thành công, đang chờ bản build mới hoặc lỗi.
- **Dọn UI hook** — gỡ các UI hook và event hook do chính loader quản lý khi panel được thay thế hoặc đóng, giúp hạn chế callback bị đăng ký lặp trong phiên làm việc dài.
- **Trạng thái, log và cache** — kiểm tra DLL đang dùng, trạng thái loader, xem lỗi và dọn các bản sao reload tạm ngay trên giao diện.
- **Giao diện Việt/Anh** — chuyển giữa `Tiếng Việt` và `English` bằng nút `VI/EN`; lựa chọn được lưu cho các phiên AutoCAD sau.
- **Hai project theo runtime** — repository có project Net48 cho AutoCAD 2021–2024 và project Net8 cho AutoCAD 2025–2026.
- **Chỉ một nơi chứa file build** — toàn bộ DLL/PDB đầu ra nằm trong `Build/`, không rải rác ở nhiều project.

## Cơ chế reload không khóa DLL

1. Bạn chọn DLL được project plugin tạo ra.
2. AutoCadDevLoader sao chép DLL đó vào một thư mục riêng trong `%TEMP%\CadDevLoader`.
3. AutoCAD nạp bản sao tạm, không nạp trực tiếp file build gốc.
4. AutoCadDevLoader quét assembly vừa nạp, tìm các lệnh hợp lệ và cập nhật panel.
5. Sau lần build thành công tiếp theo, bộ theo dõi thông báo có DLL mới. Bấm **Reload** hoặc chạy `DEVRELOAD`.

Cơ chế này xử lý vấn đề thường gặp nhất khi phát triển plugin: AutoCAD giữ khóa đúng file DLL mà trình biên dịch cần ghi đè.

> **Lưu ý quan trọng:** AutoCAD không thể unload hoàn toàn một managed assembly khỏi process hiện tại. Mỗi lần reload sẽ nạp thêm một bản sao assembly mới. AutoCadDevLoader giữ DLL nguồn ở trạng thái có thể build tiếp và dọn các UI hook do loader quản lý, nhưng không thể tự động hoàn tác mọi static state, đăng ký event, cửa sổ, timer hoặc tài nguyên do plugin của bạn tạo ra. Plugin nên khởi tạo theo cách an toàn khi gọi lặp và tự dọn tài nguyên của chính nó. Hãy khởi động lại AutoCAD khi cần một process hoàn toàn sạch.

## Tương thích và binary có sẵn

| Phiên bản AutoCAD | Nền tảng | Project/DLL loader | Tình trạng ở v1.0.0 |
|---|---|---|---|
| AutoCAD 2021–2024 | .NET Framework 4.8 | `CadDevLoader.Net48.dll` | Có binary build sẵn trong Release |
| AutoCAD 2025–2026 | .NET 8 for Windows | `CadDevLoader.Net8.dll` | Có mã nguồn; cần tự build bằng managed API AutoCAD 2025/2026 phù hợp |

**GitHub Release v1.0.0 chỉ chứa binary build sẵn `CadDevLoader.Net48.dll`.** Release hiện tại không có DLL Net8 dùng ngay vì môi trường phát hành không có bộ API AutoCAD 2025/2026 cần thiết.

Project Net8 vẫn có đầy đủ trong repository để người dùng AutoCAD 2025–2026 tự build với managed API assembly phù hợp với môi trường AutoCAD của mình. AutoCAD 2027 hiện chưa được hỗ trợ; không nên giả định binary build bằng API 2025/2026 sẽ tương thích với AutoCAD 2027.

Chỉ dùng loader phù hợp với phiên bản AutoCAD đang chạy. DLL plugin đang phát triển cũng phải target nền tảng tương thích với phiên bản AutoCAD đó.

## Bắt đầu nhanh

### AutoCAD 2021–2024: dùng binary trong Release

1. Tải gói mới nhất tại [GitHub Releases](https://github.com/Congthanhx1/AutoCadDevLoader/releases).
2. Nếu Windows chặn file tải về, mở **Properties** của file ZIP, chọn **Unblock**, rồi mới giải nén.
3. Đặt các file đã giải nén trong một thư mục cục bộ ổn định.
4. Mở AutoCAD và chạy `NETLOAD`.
5. Chọn `CadDevLoader.Net48.dll`.
6. Chạy `DEVSHOW` để mở bảng lệnh nhanh.
7. Mở menu `⋯`, chọn **Nạp/Đổi DLL**, rồi chọn DLL build của plugin đang phát triển.
8. Build plugin. Khi AutoCadDevLoader nhận diện đầu ra mới, bấm **Reload**.
9. Tìm một lệnh đã nhận diện và chạy trực tiếp từ panel.

Không dùng `NETLOAD` để nạp trực tiếp plugin đang phát triển trong cùng phiên AutoCAD. Hãy để AutoCadDevLoader nạp bản sao tạm của plugin.

### AutoCAD 2025–2026: tự build project Net8

Release v1.0.0 không cung cấp asset Net8 đã build sẵn.

1. Clone hoặc tải mã nguồn repository này.
2. Cài .NET 8 SDK và dùng managed API assembly từ bản cài đặt hoặc SDK AutoCAD 2025/2026 phù hợp.
3. Build `CadDevLoader.Net8/CadDevLoader.Net8.csproj` bằng các reference đó.
4. Trong AutoCAD, chạy `NETLOAD` và chọn `Build/CadDevLoader.Net8.dll`.
5. Chạy `DEVSHOW`, sau đó dùng quy trình Nạp/Đổi DLL và reload giống phần trên.

Xem [Build từ mã nguồn](#build-từ-mã-nguồn) để biết cấu trúc đầu ra của repository.

### Vòng lặp làm việc hằng ngày

```text
Sửa code → Build plugin → Nhận thông báo → Reload → Chạy lệnh
```

DLL đang chọn, danh sách yêu thích, lệnh gần đây và ngôn ngữ giao diện được lưu lại để lần kiểm thử tiếp theo diễn ra nhanh hơn.

## Bảng lệnh nhanh

Panel được thiết kế ngắn gọn và tập trung:

- **Reload** luôn là thao tác chính.
- **Tìm kiếm** lọc ngay danh sách lệnh đã nhận diện.
- **Yêu thích** chứa các lệnh được ghim bằng nút ngôi sao.
- **Gần đây** chứa các lệnh vừa được chạy.
- **Tất cả lệnh** chứa mọi lệnh hợp lệ trong bản plugin hiện tại.
- **Nút ngôi sao** dùng để ghim hoặc bỏ ghim một lệnh.
- **Nút `VI/EN`** trên header dùng để đổi giao diện loader giữa tiếng Việt và tiếng Anh.
- **Menu `⋯`** chứa Nạp/Đổi DLL, trạng thái/log và thao tác dọn cache.
- Khu vực lỗi được thu gọn ở cuối panel cho đến khi bạn cần xem chi tiết.

Nếu đóng panel, chạy `DEVSHOW` để mở lại.

## Các lệnh AutoCAD

| Lệnh | Chức năng |
|---|---|
| `DEVSHOW` | Mở hoặc đưa panel AutoCadDevLoader lên trước. |
| `DEVLOAD` | Yêu cầu chọn DLL plugin, nạp qua bản sao tạm và cập nhật danh sách lệnh. |
| `DEVRELOAD` | Reload DLL đang chọn và cập nhật danh sách lệnh. |
| `DEVLIST` | Liệt kê các lệnh hợp lệ đã nhận diện trong plugin hiện tại. |
| `DEVRUN` | Yêu cầu chọn và chạy một lệnh đã nhận diện. |
| `DEVSTATUS` | Hiển thị trạng thái DLL, bộ theo dõi build, reload, cache và lỗi. |

Panel và các lệnh trên dùng chung một trạng thái loader, vì vậy bạn có thể chuyển qua lại giữa giao diện và dòng lệnh bất cứ lúc nào.

## Quy tắc nhận diện lệnh

Một phương thức sẽ xuất hiện trong AutoCadDevLoader khi:

- được khai báo là lệnh AutoCAD bằng `CommandMethod`; và
- không có tham số phương thức.

Ví dụ:

```csharp
using Autodesk.AutoCAD.Runtime;

public class SampleCommands
{
    [CommandMethod("HELLODEV")]
    public void HelloDev()
    {
        // Nội dung lệnh
    }
}
```

Lệnh được tạo động, phương thức có tham số hoặc lệnh nằm trong assembly bị lỗi khi nạp sẽ không xuất hiện trên panel.

## Chuyển ngôn ngữ

Bấm nút `VI/EN` trên header của panel để chuyển giữa **English** và **Tiếng Việt**. AutoCadDevLoader cập nhật giao diện của chính nó và lưu lựa chọn cho lần mở sau. Thao tác này không đổi ngôn ngữ AutoCAD và không đổi tên lệnh do plugin đang nạp cung cấp.

## Build từ mã nguồn

### Yêu cầu

- Windows
- Một bản AutoCAD được hỗ trợ hoặc bộ managed reference assembly AutoCAD tương thích
- Bộ công cụ phát triển .NET Framework 4.8 cho loader Net48
- .NET 8 SDK và managed API assembly AutoCAD 2025/2026 phù hợp cho loader Net8

Trên máy đã cấu hình đủ Autodesk reference cần thiết, dùng điểm build chung:

```powershell
.\Build-Loaders.cmd
```

Hoặc gọi trực tiếp script PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\Build-Loaders.ps1
```

Script sẽ dò vị trí AutoCAD reference tương thích đã cài khi có thể. Nếu máy chưa có reference Net8, hãy build project Net8 trên máy có bộ API AutoCAD 2025/2026 phù hợp. Nếu chạy trực tiếp `dotnet build` và gặp lỗi không tìm thấy assembly Autodesk, hãy dùng script build hoặc truyền đúng đường dẫn cài đặt/reference AutoCAD cho MSBuild.

`Build/` chỉ chứa DLL/PDB của các target đã build thành công:

```text
Build/
├── CadDevLoader.Net48.dll
├── CadDevLoader.Net48.pdb
├── CadDevLoader.Net8.dll      # có sau khi build Net8 thành công
└── CadDevLoader.Net8.pdb      # có sau khi build Net8 thành công
```

File trung gian được đặt ngoài repository tại `%TEMP%\CadDevLoaderBuild\<ProjectName>` theo `Directory.Build.props`. Vì vậy, thư mục project không phát sinh output `bin/` hoặc `obj/` và `Build/` luôn gọn để nạp hoặc đóng gói.

## Cấu trúc repository

```text
AutoCadDevLoader/
├── Shared/
│   └── DevLoaderCommands.cs       # lệnh loader, logic reload và UI dùng chung
├── CadDevLoader.Net48/
│   └── CadDevLoader.Net48.csproj  # AutoCAD 2021–2024
├── CadDevLoader.Net8/
│   └── CadDevLoader.Net8.csproj   # AutoCAD 2025–2026
├── Directory.Build.props          # output vào Build, intermediate vào thư mục temp
├── Build-Loaders.ps1              # script build PowerShell
├── Build-Loaders.cmd              # điểm chạy build nhanh trên Windows
├── README.md
└── README.vi.md
```

`Build/` được sinh ở máy cục bộ và không được commit.

## Giới hạn và cách thiết kế plugin phù hợp

- Managed assembly đã nạp vào AutoCAD không thể bị gỡ khỏi process đó. Reload sẽ tạo và nạp một bản sao tạm mới.
- Trước khi khởi tạo bản mới, AutoCadDevLoader gọi các hàm `IExtensionApplication.Terminate()` của plugin cũ và gọi hook static không tham số `DevCleanup` hoặc `CloseAllPalettes` nếu plugin có cung cấp. Plugin vẫn phải cài đặt các đường dọn dẹp này đúng cách, tự hủy đăng ký event AutoCAD, dispose cửa sổ và timer, đồng thời tránh khởi tạo trùng.
- Thay đổi liên quan đến global/static state, khởi tạo cấp assembly, native dependency hoặc framework UI phức tạp vẫn có thể yêu cầu khởi động lại AutoCAD.
- Chỉ các phương thức `CommandMethod` không có tham số mới được tự động đưa vào danh sách.
- Managed dependency nằm cạnh DLL nguồn sẽ được sao chép vào cache tạm và resolve từ đó. Dependency vẫn phải tương thích với phiên bản AutoCAD hiện tại. Dependency cùng identity đã được AutoCAD nạp trước đó có thể vẫn trỏ tới bản cũ, vì vậy thay đổi dependency đôi khi cần tăng version assembly hoặc khởi động lại AutoCAD.
- Release v1.0.0 chỉ có binary Net48. Project Net8 phải được tự build bằng reference AutoCAD 2025/2026 phù hợp.
- Chưa xác nhận tương thích với AutoCAD 2027.
- Reload chỉ là tiện ích phát triển, không tạo môi trường cách ly process. Luôn kiểm thử bản plugin cuối trong một phiên AutoCAD mới trước khi phát hành.

## Xử lý sự cố

### `NETLOAD` từ chối loader

- Kiểm tra đã chọn đúng runtime theo bảng tương thích.
- Với AutoCAD 2025–2026, kiểm tra đã build project Net8 bằng API reference phù hợp; không nạp binary Net48 từ Release.
- Unblock file ZIP hoặc DLL tải về trong Windows Properties.
- Kiểm tra Trusted Paths và thiết lập bảo mật của AutoCAD.
- Không trộn loader Net48 và Net8 trong cùng một phiên AutoCAD.

### Không thấy DLL Net8 trong Release

Đây là trạng thái đúng của v1.0.0. Release chỉ chứa `CadDevLoader.Net48.dll`. Hãy tự build `CadDevLoader.Net8.csproj` bằng managed API assembly AutoCAD 2025/2026 phù hợp.

### DLL plugin vẫn bị khóa

- Đảm bảo DLL được chọn qua `DEVLOAD` hoặc **Nạp/Đổi DLL**.
- Không đồng thời nạp DLL plugin gốc trực tiếp bằng `NETLOAD`.
- Một process, debugger, dependency loader hoặc phần mềm diệt virus khác có thể đang giữ file; dùng `DEVSTATUS` để kiểm tra đường dẫn nguồn và đường dẫn tạm.

### Không thấy một lệnh

- Kiểm tra phương thức có `CommandMethod` và không có tham số.
- Xem khu vực lỗi hoặc màn hình trạng thái/log để tìm lỗi nạp assembly hoặc dependency.
- Kiểm tra AutoCadDevLoader đang theo dõi đúng DLL do cấu hình build hiện tại tạo ra.
- Thử reload thủ công bằng `DEVRELOAD`.

### AutoCadDevLoader vẫn báo bản build cũ

- Kiểm tra đường dẫn nguồn đang chọn có đúng với output hiện tại của project không.
- Chờ build hoàn tất rồi mới reload.
- Chạy `DEVSTATUS`, sau đó chạy `DEVRELOAD`.
- Khi cần, dùng thao tác dọn cache và khởi động lại AutoCAD để có process hoàn toàn sạch.

### Event hoặc thao tác UI chạy nhiều lần

Assembly plugin cũ vẫn còn trong AutoCAD. Hãy thiết kế phần khởi tạo của plugin an toàn khi chạy lặp và chủ động hủy đăng ký/dispose các tài nguyên do plugin tạo. Khởi động lại AutoCAD sau các thay đổi cấu trúc không thể dọn an toàn.

### Đã đóng panel

Chạy `DEVSHOW`.

### Cache tạm tăng dần

Dùng thao tác dọn cache trong menu `⋯`. File đang được process AutoCAD hiện tại sử dụng có thể chỉ xóa được sau khi thoát AutoCAD.

## Góp ý

Hãy tạo [GitHub Issue](https://github.com/Congthanhx1/AutoCadDevLoader/issues) cho lỗi có thể tái hiện hoặc đề xuất tính năng. Nên kèm phiên bản AutoCAD, runtime loader, target framework của plugin và nội dung trạng thái/lỗi liên quan.
