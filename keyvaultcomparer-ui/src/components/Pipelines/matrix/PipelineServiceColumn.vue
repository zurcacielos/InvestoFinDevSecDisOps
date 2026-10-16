<script setup lang="ts">
import { computed } from 'vue';
import type { PipelineMatrixRow } from '../../../stores/devopsDataStore';
import { useDevopsDataStore } from '../../../stores/devopsDataStore';
import { useSettingsStore } from '../../../stores/settingsStore';
import { openUrlInNewTab } from '../../../utils/urlOpener';
import { getPipelineDefinitionUrl, getRepoUrl } from '../../../utils/pipelineUrlHelpers';

const props = defineProps<{
  row: PipelineMatrixRow;
}>();

const emit = defineEmits<{
  (e: 'open-context-menu', event: MouseEvent, row: PipelineMatrixRow, cell: any, envName: string): void;
  (e: 'open-all-yamls', row: PipelineMatrixRow): void;
}>();

const devopsStore = useDevopsDataStore();
const settingsStore = useSettingsStore();

const definitionUrl = computed(() => {
  return getPipelineDefinitionUrl(devopsStore.organization, devopsStore.project, props.row.pipelineId);
});

const repoUrl = computed(() => {
  return getRepoUrl(
    props.row.repository,
    settingsStore.uiSettings.githubBaseUrl,
    settingsStore.uiSettings.useGithubOrg,
    settingsStore.uiSettings.githubOrg
  );
});

const cleanBranch = computed(() => {
  const b = props.row.repository?.defaultBranch;
  return b ? b.replace('refs/heads/', '') : null;
});
</script>

<template>
  <td 
    @contextmenu="emit('open-context-menu', $event, row, undefined, '')"
    class="px-3.5 py-2.5 border-r border-slate-200 bg-white sticky left-0 z-5 shadow-[2px_0_4px_rgba(0,0,0,0.03)] cursor-context-menu align-top min-w-[220px] max-w-[260px]"
  >
    <div class="flex flex-col gap-1.5">
      <!-- Top Line: Pipeline Name & Action Icons -->
      <div class="flex items-start justify-between gap-1.5">
        <div class="min-w-0 flex-1">
          <button 
            @click="openUrlInNewTab(definitionUrl)"
            class="font-bold text-slate-900 hover:text-blue-600 hover:underline text-left text-xs leading-snug truncate block w-full"
            :title="`Open Pipeline '${row.pipelineName}' in Azure DevOps`"
          >
            {{ row.pipelineName }}
          </button>
          <div v-if="row.yamlFilename" class="flex items-center gap-1.5 text-[10px] text-slate-400 font-mono mt-0.5">
            <span class="truncate" :title="`YAML: ${row.yamlFilename}`">
              {{ row.yamlFilename }}
            </span>
          </div>
        </div>

        <!-- Quick Action Buttons -->
        <div class="flex items-center gap-0.5 shrink-0">
          <!-- Refresh Single Pipeline -->
          <button 
            @click.stop="devopsStore.refreshSinglePipeline(row.pipelineId)"
            class="p-1 rounded text-slate-400 hover:text-blue-600 hover:bg-blue-50 transition-colors"
            :title="`Refresh '${row.pipelineName}' (Scan depth: ${devopsStore.scanDepth} runs)`"
            :disabled="devopsStore.isSinglePipelineRefreshing(row.pipelineId)"
          >
            <svg 
              xmlns="http://www.w3.org/2000/svg" 
              class="h-3.5 w-3.5" 
              :class="{'animate-spin text-blue-600': devopsStore.isSinglePipelineRefreshing(row.pipelineId)}"
              fill="none" 
              viewBox="0 0 24 24" 
              stroke="currentColor"
            >
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
          </button>

          <!-- Open all GitHub Yaml -->
          <button 
            @click.stop="emit('open-all-yamls', row)"
            class="p-1 rounded text-slate-400 hover:text-violet-700 hover:bg-violet-50 transition-colors"
            title="Open all GitHub YAML files (Main pipeline &amp; nested templates)"
          >
            <svg class="h-3.5 w-3.5 text-slate-700" viewBox="0 0 24 24" fill="currentColor">
              <path fill-rule="evenodd" clip-rule="evenodd" d="M12 2C6.477 2 2 6.484 2 12.017c0 4.425 2.865 8.18 6.839 9.504.5.092.682-.217.682-.483 0-.237-.008-.868-.013-1.703-2.782.605-3.369-1.343-3.369-1.343-.454-1.158-1.11-1.466-1.11-1.466-.908-.62.069-.608.069-.608 1.003.07 1.53 1.032 1.53 1.032.892 1.53 2.341 1.088 2.91.832.092-.647.35-1.088.636-1.338-2.22-.253-4.555-1.113-4.555-4.951 0-1.093.39-1.988 1.029-2.688-.103-.253-.446-1.272.098-2.65 0 0 .84-.27 2.75 1.026A9.564 9.564 0 0112 6.844c.85.004 1.705.115 2.504.337 1.909-1.296 2.747-1.027 2.747-1.027.546 1.379.202 2.398.1 2.651.64.7 1.028 1.595 1.028 2.688 0 3.848-2.339 4.695-4.566 4.943.359.309.678.92.678 1.855 0 1.338-.012 2.419-.012 2.747 0 .268.18.58.688.482A10.019 10.019 0 0022 12.017C22 6.484 17.522 2 12 2z" />
            </svg>
          </button>

          <!-- Copy Resolved Full YAML -->
          <button 
            @click.stop="devopsStore.copyPipelineYamlToClipboard(row.pipelineId, row.pipelineName)"
            class="p-1 rounded text-slate-400 hover:text-blue-600 hover:bg-blue-50 transition-colors"
            title="Copy full pipeline YAML (resolved nested templates) to clipboard"
            :disabled="devopsStore.copyingYamlPipelineId === row.pipelineId"
          >
            <svg v-if="devopsStore.copyingYamlPipelineId === row.pipelineId" class="animate-spin h-3.5 w-3.5 text-blue-500" fill="none" viewBox="0 0 24 24">
              <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
              <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
            </svg>
            <svg v-else xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z" />
            </svg>
          </button>

          <!-- View YAML Modal -->
          <button 
            @click.stop="devopsStore.openYamlModal(row.pipelineId, row.pipelineName)"
            class="p-1 rounded text-slate-400 hover:text-emerald-600 hover:bg-emerald-50 transition-colors"
            title="View resolved pipeline YAML"
          >
            <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
            </svg>
          </button>
        </div>
      </div>

      <!-- Bottom Line: Integrated Repository & Branch Metadata -->
      <div v-if="row.repository" class="flex items-center gap-1.5 text-[11px] text-slate-600 bg-slate-50/80 px-2 py-1 rounded border border-slate-200/60 min-w-0">
        <svg v-if="row.repository.type?.toLowerCase() === 'github'" class="h-3 w-3 text-slate-700 shrink-0" viewBox="0 0 24 24" fill="currentColor">
          <path fill-rule="evenodd" clip-rule="evenodd" d="M12 2C6.477 2 2 6.484 2 12.017c0 4.425 2.865 8.18 6.839 9.504.5.092.682-.217.682-.483 0-.237-.008-.868-.013-1.703-2.782.605-3.369-1.343-3.369-1.343-.454-1.158-1.11-1.466-1.11-1.466-.908-.62.069-.608.069-.608 1.003.07 1.53 1.032 1.53 1.032.892 1.53 2.341 1.088 2.91.832.092-.647.35-1.088.636-1.338-2.22-.253-4.555-1.113-4.555-4.951 0-1.093.39-1.988 1.029-2.688-.103-.253-.446-1.272.098-2.65 0 0 .84-.27 2.75 1.026A9.564 9.564 0 0112 6.844c.85.004 1.705.115 2.504.337 1.909-1.296 2.747-1.027 2.747-1.027.546 1.379.202 2.398.1 2.651.64.7 1.028 1.595 1.028 2.688 0 3.848-2.339 4.695-4.566 4.943.359.309.678.92.678 1.855 0 1.338-.012 2.419-.012 2.747 0 .268.18.58.688.482A10.019 10.019 0 0022 12.017C22 6.484 17.522 2 12 2z" />
        </svg>
        <svg v-else class="h-3 w-3 text-blue-600 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z" />
        </svg>

        <button 
          v-if="repoUrl"
          @click="openUrlInNewTab(repoUrl)"
          class="font-medium text-slate-700 hover:text-blue-600 hover:underline truncate"
          :title="`Open Repository '${row.repository.name}'`"
        >
          {{ row.repository.name }}
        </button>
        <span v-else class="text-slate-600 truncate" :title="row.repository.name">
          {{ row.repository.name }}
        </span>

        <span v-if="cleanBranch" class="text-slate-300 font-mono text-[10px]">&bull;</span>
        <span v-if="cleanBranch" class="text-[10px] text-slate-500 font-mono truncate" :title="`Branch: ${cleanBranch}`">
          {{ cleanBranch }}
        </span>
      </div>
    </div>
  </td>
</template>
