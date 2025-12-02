# WallHitSound

壁に衝突したときに音を鳴らすMODです。

## 設定画面
<img width="40%" height="40%" alt="image" src="https://github.com/user-attachments/assets/929dab24-67ee-43d5-8447-8c83e0cef1bd" />  

<small>※画像は開発中のものです。</small> 
- **Enabled**：Modの有効/無効を切り替えます。

- **Volume**：beep音、または設定した音のボリュームを変更します。

- **Sound Type**：壁に衝突したときに鳴らしたい音
　beep：beep音
  また鳴らしたい音をUserData\WallHitSoundフォルダに格納することでプルダウンから選択可能になります。
  対応音源は、wav、ogg、mp3になります。
  WallHitSoundが作成される際に初期音源としてdeep_impact.wav、wall_hit.wavが生成されます。

- **Beep Frequency(Hz)**：beep音の周波数を変更します。  
　※Sound Typeでbeepを選択した場合のみの値が有効になります。

<img width="40%" height="40%" alt="image" src="https://github.com/user-attachments/assets/124b5154-861f-417f-b19e-c403d88cb9fd" />  

- **pitch**：beep音や設定した音のピッチを変更します。
