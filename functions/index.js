const functions = require("firebase-functions");
const { defineSecret } = require("firebase-functions/params");
const admin = require("firebase-admin");
const cors = require("cors")({ origin: true });
const FormData = require("form-data");
const axios = require("axios");

// Initialize Firebase Admin
admin.initializeApp();

// Firestore reference
const db = admin.firestore();

const OPENAI_BASE_URL = "https://api.openai.com/v1";
const OPENAI_API_KEY = defineSecret("OPENAI_API_KEY");

// ═══════════════════════════════════════════════════════════════════════
// Helper: Get OpenAI API Key
// ═══════════════════════════════════════════════════════════════════════
function getOpenAIApiKey() {
  // Secret Manager (deployed)
  if (OPENAI_API_KEY.value()) {
    return OPENAI_API_KEY.value();
  }

  // Environment variable (local testing / emulator)
  if (process.env.OPENAI_API_KEY) {
    return process.env.OPENAI_API_KEY;
  }
  
  throw new Error("OpenAI API key not configured");
}

// ═══════════════════════════════════════════════════════════════════════
// API: Transcribe Audio (Whisper)
// POST /transcribe
// Body: { audioBase64: string, fileName: string, language?: string }
// ═══════════════════════════════════════════════════════════════════════
exports.transcribe = functions
  .runWith({ secrets: [OPENAI_API_KEY] })
  .https.onRequest(async (req, res) => {
  return cors(req, res, async () => {
    try {
      if (req.method !== "POST") {
        return res.status(405).json({ error: "Method not allowed" });
      }

      const { audioBase64, fileName, language } = req.body;

      if (!audioBase64 || !fileName) {
        return res.status(400).json({ error: "Missing audioBase64 or fileName" });
      }

      const apiKey = getOpenAIApiKey();
      const audioBuffer = Buffer.from(audioBase64, "base64");

      // Create form data
      const formData = new FormData();
      formData.append("file", audioBuffer, {
        filename: fileName,
        contentType: "audio/m4a",
      });
      formData.append("model", "whisper-1");
      if (language) {
        formData.append("language", language);
      }

      // Forward to OpenAI
      const response = await axios.post(
        `${OPENAI_BASE_URL}/audio/transcriptions`,
        formData,
        {
          headers: {
            "Authorization": `Bearer ${apiKey}`,
            ...formData.getHeaders(),
          },
        }
      );

      return res.status(200).json({ text: response.data.text || "" });

    } catch (error) {
      console.error("Transcribe error:", error.message);
      const status = error.response?.status || 500;
      const errorMsg = error.response?.data?.error?.message || error.message;
      return res.status(status).json({ error: errorMsg });
    }
  });
});

// ═══════════════════════════════════════════════════════════════════════
// API: Chat Completions (GPT)
// POST /chat
// Body: { messages: Array, model?: string, temperature?: number, maxTokens?: number, responseFormat?: object }
// ═══════════════════════════════════════════════════════════════════════
exports.chat = functions
  .runWith({ secrets: [OPENAI_API_KEY] })
  .https.onRequest(async (req, res) => {
  return cors(req, res, async () => {
    try {
      if (req.method !== "POST") {
        return res.status(405).json({ error: "Method not allowed" });
      }

      const { messages, model, temperature, maxTokens, responseFormat } = req.body;

      if (!messages || !Array.isArray(messages)) {
        return res.status(400).json({ error: "Missing or invalid messages array" });
      }

      const apiKey = getOpenAIApiKey();

      // Forward to OpenAI
      const response = await axios.post(
        `${OPENAI_BASE_URL}/chat/completions`,
        {
          model: model || "gpt-4o-mini",
          messages: messages,
          temperature: temperature !== undefined ? temperature : 0.7,
          max_tokens: maxTokens || 512,
          ...(responseFormat && { response_format: responseFormat }),
        },
        {
          headers: {
            "Authorization": `Bearer ${apiKey}`,
            "Content-Type": "application/json",
          },
        }
      );

      return res.status(200).json(response.data);

    } catch (error) {
      console.error("Chat error:", error.message);
      const status = error.response?.status || 500;
      const errorMsg = error.response?.data?.error?.message || error.message;
      return res.status(status).json({ error: errorMsg });
    }
  });
});

// ═══════════════════════════════════════════════════════════════════════
// API: Classify Intent (GPT with system prompt)
// POST /classifyIntent
// Body: { transcript: string }
// ═══════════════════════════════════════════════════════════════════════
exports.classifyIntent = functions
  .runWith({ secrets: [OPENAI_API_KEY] })
  .https.onRequest(async (req, res) => {
  return cors(req, res, async () => {
    try {
      if (req.method !== "POST") {
        return res.status(405).json({ error: "Method not allowed" });
      }

      const { transcript } = req.body;

      if (!transcript) {
        return res.status(400).json({ error: "Missing transcript" });
      }

      const apiKey = getOpenAIApiKey();

      // System prompt (from Constants.cs IntentPrompts.SystemPrompt)
      const systemPrompt = `You are RUNA, a voice assistant for a navigation app designed for blind users.
Your task is to classify the user's intent and extract relevant parameters.

Available actions:
- NavigateTo: User wants to navigate to a destination. Extract 'destination' parameter.
- GetDirections: User wants directions to a place. Extract 'destination' parameter.
- StartNavigation: User wants to start the current navigation.
- StopNavigation: User wants to stop navigation.
- WhereAmI: User asks about their current location.
- StartDetection: User wants to start obstacle detection / know surroundings.
- StopDetection: User wants to stop obstacle detection.
- DescribeSurroundings: User wants a description of what's around them.
- CheckStatus: User wants to know app status.
- GetHelp: User asks for help or what the app can do.
- RepeatLastMessage: User wants to hear the last message again.
- Confirm: User confirms an action (yes, okay, sure).
- Cancel: User cancels an action (no, cancel, stop).
- Unknown: Cannot determine intent.

Respond in JSON format:
{
  "action": "ActionName",
  "confidence": 0.95,
  "parameters": { "destination": "value" },
  "response": "Friendly response to speak to the user",
  "requiresFollowUp": false,
  "followUpQuestion": null
}

Be concise and helpful. Remember the user is blind, so provide clear audio feedback.`;

      // Forward to OpenAI
      const response = await axios.post(
        `${OPENAI_BASE_URL}/chat/completions`,
        {
          model: "gpt-4o-mini",
          messages: [
            { role: "system", content: systemPrompt },
            { role: "user", content: transcript }
          ],
          temperature: 0.3,
          max_tokens: 256,
          response_format: { type: "json_object" },
        },
        {
          headers: {
            "Authorization": `Bearer ${apiKey}`,
            "Content-Type": "application/json",
          },
        }
      );

      const messageContent = response.data.choices?.[0]?.message?.content;
      
      if (!messageContent) {
        return res.status(500).json({ error: "No response from GPT" });
      }

      // Parse and return JSON
      const classification = JSON.parse(messageContent);
      return res.status(200).json(classification);

    } catch (error) {
      console.error("Classify intent error:", error.message);
      const status = error.response?.status || 500;
      const errorMsg = error.response?.data?.error?.message || error.message;
      return res.status(status).json({ error: errorMsg });
    }
  });
});

// ═══════════════════════════════════════════════════════════════════════
// Helper: Verify Firebase ID Token
// ═══════════════════════════════════════════════════════════════════════
async function verifyIdToken(req) {
  const authHeader = req.headers.authorization;
  if (!authHeader || !authHeader.startsWith("Bearer ")) {
    throw new Error("No authorization token provided");
  }

  const idToken = authHeader.split("Bearer ")[1];
  const decodedToken = await admin.auth().verifyIdToken(idToken);
  return decodedToken;
}

// ═══════════════════════════════════════════════════════════════════════
// API: Get User Profile
// GET /getUserProfile
// Headers: Authorization: Bearer <idToken>
// ═══════════════════════════════════════════════════════════════════════
exports.getUserProfile = functions.https.onRequest(async (req, res) => {
  return cors(req, res, async () => {
    try {
      if (req.method !== "GET" && req.method !== "POST") {
        return res.status(405).json({ error: "Method not allowed" });
      }

      // Verify token
      const decodedToken = await verifyIdToken(req);
      const userId = decodedToken.uid;

      // Get user profile from Firestore
      const userDoc = await db.collection("users").doc(userId).get();

      if (!userDoc.exists) {
        // Create profile if it doesn't exist
        const email = decodedToken.email || "";
        const displayName = decodedToken.name || email.split("@")[0] || "User";

        const newProfile = {
          email: email,
          displayName: displayName,
          createdAt: admin.firestore.FieldValue.serverTimestamp(),
        };

        await db.collection("users").doc(userId).set(newProfile);

        return res.status(200).json({
          userId: userId,
          email: email,
          displayName: displayName,
          createdAt: new Date().toISOString(),
        });
      }

      const userData = userDoc.data();
      return res.status(200).json({
        userId: userId,
        email: userData.email || decodedToken.email || "",
        displayName: userData.displayName || "",
        createdAt: userData.createdAt?.toDate()?.toISOString() || null,
      });

    } catch (error) {
      console.error("Get user profile error:", error.message);

      if (error.code === "auth/id-token-expired") {
        return res.status(401).json({ error: "Token expired" });
      }
      if (error.code === "auth/argument-error" || error.message.includes("authorization")) {
        return res.status(401).json({ error: "Unauthorized" });
      }

      return res.status(500).json({ error: error.message });
    }
  });
});

// ═══════════════════════════════════════════════════════════════════════
// API: Get User Settings
// GET /getUserSettings
// Headers: Authorization: Bearer <idToken>
// ═══════════════════════════════════════════════════════════════════════
exports.getUserSettings = functions.https.onRequest(async (req, res) => {
  return cors(req, res, async () => {
    try {
      if (req.method !== "GET" && req.method !== "POST") {
        return res.status(405).json({ error: "Method not allowed" });
      }

      // Verify token
      const decodedToken = await verifyIdToken(req);
      const userId = decodedToken.uid;

      // Get user settings from Firestore
      const settingsDoc = await db
        .collection("users")
        .doc(userId)
        .collection("settings")
        .doc("preferences")
        .get();

      if (!settingsDoc.exists) {
        // Return default settings
        return res.status(200).json({
          voiceFeedbackEnabled: true,
          hapticFeedbackEnabled: true,
          sensitivityMode: "medium",
        });
      }

      const settingsData = settingsDoc.data();
      return res.status(200).json({
        voiceFeedbackEnabled: settingsData.voiceFeedbackEnabled ?? true,
        hapticFeedbackEnabled: settingsData.hapticFeedbackEnabled ?? true,
        sensitivityMode: settingsData.sensitivityMode || "medium",
      });

    } catch (error) {
      console.error("Get user settings error:", error.message);

      if (error.code === "auth/id-token-expired") {
        return res.status(401).json({ error: "Token expired" });
      }
      if (error.code === "auth/argument-error" || error.message.includes("authorization")) {
        return res.status(401).json({ error: "Unauthorized" });
      }

      return res.status(500).json({ error: error.message });
    }
  });
});

// ═══════════════════════════════════════════════════════════════════════
// API: Save User Settings
// POST /saveUserSettings
// Headers: Authorization: Bearer <idToken>
// Body: { voiceFeedbackEnabled: boolean, hapticFeedbackEnabled: boolean, sensitivityMode: string }
// ═══════════════════════════════════════════════════════════════════════
exports.saveUserSettings = functions.https.onRequest(async (req, res) => {
  return cors(req, res, async () => {
    try {
      if (req.method !== "POST") {
        return res.status(405).json({ error: "Method not allowed" });
      }

      // Verify token
      const decodedToken = await verifyIdToken(req);
      const userId = decodedToken.uid;

      const { voiceFeedbackEnabled, hapticFeedbackEnabled, sensitivityMode } = req.body;

      // Validate input
      const settings = {
        voiceFeedbackEnabled: voiceFeedbackEnabled === true || voiceFeedbackEnabled === "true",
        hapticFeedbackEnabled: hapticFeedbackEnabled === true || hapticFeedbackEnabled === "true",
        sensitivityMode: ["low", "medium", "high"].includes(sensitivityMode) ? sensitivityMode : "medium",
        updatedAt: admin.firestore.FieldValue.serverTimestamp(),
      };

      // Save to Firestore
      await db
        .collection("users")
        .doc(userId)
        .collection("settings")
        .doc("preferences")
        .set(settings, { merge: true });

      return res.status(200).json({
        success: true,
        message: "Settings saved successfully",
        settings: {
          voiceFeedbackEnabled: settings.voiceFeedbackEnabled,
          hapticFeedbackEnabled: settings.hapticFeedbackEnabled,
          sensitivityMode: settings.sensitivityMode,
        },
      });

    } catch (error) {
      console.error("Save user settings error:", error.message);

      if (error.code === "auth/id-token-expired") {
        return res.status(401).json({ error: "Token expired" });
      }
      if (error.code === "auth/argument-error" || error.message.includes("authorization")) {
        return res.status(401).json({ error: "Unauthorized" });
      }

      return res.status(500).json({ error: error.message });
    }
  });
});

// ═══════════════════════════════════════════════════════════════════════
// API: Create/Update User Profile
// POST /createUserProfile
// Headers: Authorization: Bearer <idToken>
// Body: { displayName?: string }
// ═══════════════════════════════════════════════════════════════════════
exports.createUserProfile = functions.https.onRequest(async (req, res) => {
  return cors(req, res, async () => {
    try {
      if (req.method !== "POST") {
        return res.status(405).json({ error: "Method not allowed" });
      }

      // Verify token
      const decodedToken = await verifyIdToken(req);
      const userId = decodedToken.uid;
      const email = decodedToken.email || "";

      const { displayName } = req.body;

      // Check if profile exists
      const userDoc = await db.collection("users").doc(userId).get();

      const profileData = {
        email: email,
        displayName: displayName || email.split("@")[0] || "User",
        updatedAt: admin.firestore.FieldValue.serverTimestamp(),
      };

      if (!userDoc.exists) {
        profileData.createdAt = admin.firestore.FieldValue.serverTimestamp();
      }

      await db.collection("users").doc(userId).set(profileData, { merge: true });

      return res.status(200).json({
        success: true,
        message: "Profile saved successfully",
        profile: {
          userId: userId,
          email: email,
          displayName: profileData.displayName,
        },
      });

    } catch (error) {
      console.error("Create user profile error:", error.message);

      if (error.code === "auth/id-token-expired") {
        return res.status(401).json({ error: "Token expired" });
      }
      if (error.code === "auth/argument-error" || error.message.includes("authorization")) {
        return res.status(401).json({ error: "Unauthorized" });
      }

      return res.status(500).json({ error: error.message });
    }
  });
});
