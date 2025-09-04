# 🏟️ STADIUMX — VR Football & Stadium Safety Simulator

**STADIUMX** is a dual-mode 3D Unity project that combines **entertainment** and **serious simulation**.  
It features a **5v5 football game with VR/pose detection** and a **stadium safety simulator** to explore crowd dynamics and emergency response.

---

## 🎮 Modes

### ⚽ Football Mode
- 5v5 mini-football (futsal style).
- Multiplayer networking with smooth 3D camera and VFX.
- AI teammates and opponents.
- Optional **VR/AR support** for immersive gameplay.
- Experimental **pose detection** (control via body movements).

### 🚨 SafetySim Mode
- Simulates crowd dynamics in a virtual stadium.
- Training for disaster scenarios: blackout, smoke, blocked exits.
- “Commander” view to place signs, open gates, and guide evacuation.
- Based on **Green Guide principles** and **social force / crowd dynamics models**.

---

## 🛠️ Tech Stack
- **Unity 3D** (game engine).  
- **Nakama / Unity Networking** for multiplayer.  
- **XR Interaction Toolkit / OpenXR** for VR/AR.  
- **MediaPipe / BlazePose / MoveNet** for pose detection.  
- **NavMesh & crowd simulation** for evacuation scenarios.  

---

## 📌 Features Roadmap
- [x] Basic football gameplay prototype.  
- [ ] Multiplayer networking integration.  
- [ ] VR/AR support.  
- [ ] Pose detection training mode.  
- [ ] Crowd simulation for SafetySim.  
- [ ] Full evacuation scenarios with scoring/evaluation.  

---

## 🚀 Getting Started

### Requirements
- Unity 2021.3+  
- Docker (for Nakama server, optional)  
- VR headset (Oculus / Quest / other OpenXR-compatible)  

### Run Locally
```bash
# Clone repo
git clone https://github.com/<your-username>/STADIUMX.git

# Open project in Unity Hub
# Press Play to run prototype
