# ImaikeMatsuriCompassEditor.Wpf

今池まつり Compass の運営・データ確認用 WPF デスクトップアプリです。

## 目的

- 今池まつりの会場情報を確認・編集
- 公式タイムテーブルとの照合
- タイムスケジュールの確認・修正
- スケジュールへのカテゴリ付与
- 確認済みフラグによる校正状況の管理
- Supabase のイベントデータとの連携

## 技術構成

- C# / WPF
- .NET 9 (`net9.0-windows`)
- Visual Studio 2022/2026 対応
- Supabase

## 関連プロジェクト

公開Web/PWA: https://github.com/dimmsz/ImaikeMatsuriCompass

このアプリは公開Webアプリとは分離した、編集・校正用のWindowsアプリです。
