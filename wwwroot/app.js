document.addEventListener("DOMContentLoaded", () => {
  loadBlocks();
  const reloadBtn = document.getElementById("reloadBtn");
  if (reloadBtn) {
    reloadBtn.addEventListener("click", loadBlocks);
  }
});

// メッセージ表示ヘルパー
function showMessage(text, isError = false) {
  const msgEl = document.getElementById("message");
  if (!msgEl) return;
  msgEl.textContent = text;
  msgEl.className = `alert ${isError ? "alert-danger" : "alert-success"}`;
  msgEl.style.display = "block";
  setTimeout(() => {
    msgEl.style.display = "none";
  }, 5000);
}

// 日時フォーマット関数（ISO文字列 -> 日本時間の読みやすい表示）
function formatDate(isoString) {
  if (!isoString) return "-";
  const date = new Date(isoString);
  if (isNaN(date.getTime())) return isoString;
  return date.toLocaleString("ja-JP", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  });
}

// ブロック一覧取得 API 呼び出し
async function loadBlocks() {
  try {
    const response = await fetch("/api/blocks");
    if (!response.ok) throw new Error("一覧の取得に失敗しました。");
    const blocks = await response.json();

    const tbody = document.getElementById("blockTableBody");
    tbody.innerHTML = "";

    blocks.forEach((block) => {
      const id = block.id;
      const displayName = block.processName
        ? `${block.blockCode}`
        : block.blockCode;
      const status = block.status ?? "未着手";
      const rawUpdatedAt = block.updatedAt ?? "";
      const formattedUpdatedAt = formatDate(rawUpdatedAt);

      const tr = document.createElement("tr");
      tr.innerHTML = `
        <td>${id}</td>
        <td>${displayName}</td>
        <td>
            <select id="status-${id}">
                <option value="未着手" ${status === "未着手" ? "selected" : ""}>未着手</option>
                <option value="加工中" ${status === "加工中" ? "selected" : ""}>加工中</option>
                <option value="組立中" ${status === "組立中" ? "selected" : ""}>組立中</option>
                <option value="塗装中" ${status === "塗装中" ? "selected" : ""}>塗装中</option>
                <option value="完成" ${status === "完成" ? "selected" : ""}>完成</option>
            </select>
        </td>
        <td>${formattedUpdatedAt}</td>
        <td>
            <button onclick="updateStatus(${id}, '${rawUpdatedAt}')">更新</button>
        </td>
      `;
      tbody.appendChild(tr);
    });
  } catch (err) {
    showMessage(err.message, true);
  }
}

// HTMLのボタンから呼ばれる関数
async function updateStatus(id, currentUpdatedAt) {
  const selectEl = document.getElementById(`status-${id}`);
  if (!selectEl) return;
  const newStatus = selectEl.value;

  await updateBlockStatus(id, newStatus, currentUpdatedAt);
}

// ステータス更新 API 呼び出し処理
async function updateBlockStatus(id, newStatus, currentUpdatedAt) {
  try {
    const response = await fetch("/api/blocks/status", {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        id: id,
        status: newStatus,
        currentUpdatedAt: currentUpdatedAt,
      }),
    });

    if (response.ok) {
      showMessage("ステータスを更新しました！");
      await loadBlocks(); // 最新一覧を再取得・再描画
    } else if (response.status === 409) {
      const errorText = await response.text();
      alert(`【衝突エラー】${errorText}`);
      await loadBlocks(); // 衝突時も最新データに再同期
    } else {
      const errorText = await response.text();
      showMessage(`更新失敗: ${errorText}`, true);
    }
  } catch (err) {
    showMessage(`通信エラー: ${err.message}`, true);
  }
}
