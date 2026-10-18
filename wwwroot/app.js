let allBlocksCache = []; // 取得した全ブロックデータを保持するキャッシュ

document.addEventListener("DOMContentLoaded", () => {
  loadBlocks();
  initModal();

  const reloadBtn = document.getElementById("reloadBtn");
  if (reloadBtn) {
    reloadBtn.addEventListener("click", loadBlocks);
  }

  const shipFilter = document.getElementById("shipFilter");
  if (shipFilter) {
    shipFilter.addEventListener("change", renderTable);
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

// 日時フォーマット関数
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

// ステータスに応じたバッジクラスの取得
function getStatusBadgeClass(status) {
  if (status === "完成") return "badge-completed";
  if (status === "未着手") return "badge-not-started";
  return "badge-in-progress"; // 加工中、組立中、塗装中など
}

// モーダルの開閉制御初期化
function initModal() {
  const modal = document.getElementById("blockModal");
  const openBtn = document.getElementById("openModalBtn");
  const closeBtn = document.getElementById("closeModalBtn");
  const form = document.getElementById("blockForm");

  if (openBtn) {
    openBtn.addEventListener("click", () => {
      modal.style.display = "block";
    });
  }

  if (closeBtn) {
    closeBtn.addEventListener("click", () => {
      modal.style.display = "none";
    });
  }

  window.addEventListener("click", (e) => {
    if (e.target === modal) {
      modal.style.display = "none";
    }
  });

  if (form) {
    form.addEventListener("submit", async (e) => {
      e.preventDefault();

      const blockData = {
        blockCode: document.getElementById("inputBlockCode").value.trim(),
        shipId: document.getElementById("inputShipId").value.trim(),
        processName: document.getElementById("inputProcessName").value.trim(),
        weightTons:
          parseInt(document.getElementById("inputWeightTons").value, 10) || 0,
      };

      try {
        const response = await fetch("/api/blocks", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(blockData),
        });

        if (response.ok) {
          showMessage("新しいブロックを詳細登録しました！");
          modal.style.display = "none";
          form.reset();
          document.getElementById("inputShipId").value = "S-001";
          document.getElementById("inputProcessName").value = "切断";
          document.getElementById("inputWeightTons").value = "5";
          await loadBlocks();
        } else {
          const errorText = await response.text();
          showMessage(`追加失敗: ${errorText}`, true);
        }
      } catch (err) {
        showMessage(`通信エラー: ${err.message}`, true);
      }
    });
  }
}

// ブロック一覧取得 API 呼び出し
async function loadBlocks() {
  try {
    const response = await fetch("/api/blocks");
    if (!response.ok) throw new Error("一覧の取得に失敗しました。");
    allBlocksCache = await response.json();

    updateShipFilterOptions();
    renderTable();
  } catch (err) {
    showMessage(err.message, true);
  }
}

// 船舶フィルターの選択肢を動的に更新
function updateShipFilterOptions() {
  const shipFilter = document.getElementById("shipFilter");
  if (!shipFilter) return;

  const currentSelected = shipFilter.value;
  const shipIds = [
    ...new Set(allBlocksCache.map((b) => b.shipId).filter(Boolean)),
  ];

  shipFilter.innerHTML = '<option value="ALL">すべて表示</option>';
  shipIds.forEach((shipId) => {
    const opt = document.createElement("option");
    opt.value = shipId;
    opt.textContent = shipId;
    shipFilter.appendChild(opt);
  });

  // 以前の選択状態を維持
  if (shipIds.includes(currentSelected)) {
    shipFilter.value = currentSelected;
  }
}

// テーブルの描画（フィルタリング適用）
function renderTable() {
  const tbody = document.getElementById("blockTableBody");
  const shipFilter = document.getElementById("shipFilter");
  const selectedShip = shipFilter ? shipFilter.value : "ALL";

  tbody.innerHTML = "";

  const filteredBlocks = allBlocksCache.filter((block) => {
    if (selectedShip === "ALL") return true;
    return block.shipId === selectedShip;
  });

  filteredBlocks.forEach((block) => {
    const id = block.id;
    const blockCode = block.blockCode ?? "";
    const shipId = block.shipId ?? "-";
    const processName = block.processName ?? "-";
    const weightTons = block.weightTons ?? 0;
    const status = block.status ?? "未着手";
    const rawUpdatedAt = block.updatedAt ?? "";
    const formattedUpdatedAt = formatDate(rawUpdatedAt);
    const badgeClass = getStatusBadgeClass(status);

    const tr = document.createElement("tr");
    tr.innerHTML = `
      <td>${id}</td>
      <td>${blockCode}</td>
      <td><strong>${shipId}</strong></td>
      <td>${processName}</td>
      <td>${weightTons} t</td>
      <td>
          <span class="badge ${badgeClass}" style="margin-right: 8px;">${status}</span>
          <select id="status-${id}" style="font-size: 0.9em;">
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
          <button class="btn-danger" onclick="deleteBlock(${id})">削除</button>
      </td>
    `;
    tbody.appendChild(tr);
  });
}

// ブロック削除処理 (DELETE /api/blocks/{id})
async function deleteBlock(id) {
  if (!confirm(`ID: ${id} のブロックを削除しますか？`)) return;

  try {
    const response = await fetch(`/api/blocks/${id}`, {
      method: "DELETE",
    });

    if (response.ok) {
      showMessage("ブロックを削除しました！");
      await loadBlocks();
    } else {
      const errorText = await response.text();
      showMessage(`削除失敗: ${errorText}`, true);
    }
  } catch (err) {
    showMessage(`通信エラー: ${err.message}`, true);
  }
}

// ステータス更新
async function updateStatus(id, currentUpdatedAt) {
  const selectEl = document.getElementById(`status-${id}`);
  if (!selectEl) return;
  const newStatus = selectEl.value;

  await updateBlockStatus(id, newStatus, currentUpdatedAt);
}

// ステータス更新 API 呼び出し処理 (PUT /api/blocks/status)
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
      await loadBlocks();
    } else if (response.status === 409) {
      const errorText = await response.text();
      alert(`【衝突エラー】${errorText}`);
      await loadBlocks();
    } else {
      const errorText = await response.text();
      showMessage(`更新失敗: ${errorText}`, true);
    }
  } catch (err) {
    showMessage(`通信エラー: ${err.message}`, true);
  }
}
