"""
Background Agent System for Autonomous Task Processing
Inspired by Cursor's background agents that work independently
"""

import asyncio
import logging
import time
from typing import Dict, List, Optional, Any, Callable
from dataclasses import dataclass
from enum import Enum
from datetime import datetime, timedelta
import json
import threading
from queue import Queue, Empty

logger = logging.getLogger(__name__)

class TaskPriority(Enum):
    """Task priority levels"""
    LOW = 1
    MEDIUM = 2
    HIGH = 3
    CRITICAL = 4

class TaskStatus(Enum):
    """Task status tracking"""
    PENDING = "pending"
    RUNNING = "running"
    COMPLETED = "completed"
    FAILED = "failed"
    CANCELLED = "cancelled"

@dataclass
class BackgroundTask:
    """Background task definition"""
    task_id: str
    task_type: str
    priority: TaskPriority
    payload: Dict[str, Any]
    created_at: datetime
    started_at: Optional[datetime] = None
    completed_at: Optional[datetime] = None
    status: TaskStatus = TaskStatus.PENDING
    result: Optional[Any] = None
    error: Optional[str] = None
    retry_count: int = 0
    max_retries: int = 3
    timeout_seconds: int = 300

class BackgroundAgentManager:
    """Manages background agents for autonomous task processing"""
    
    def __init__(self, max_workers: int = 5):
        self.max_workers = max_workers
        self.task_queue = Queue()
        self.workers = []
        self.running_tasks: Dict[str, BackgroundTask] = {}
        self.completed_tasks: Dict[str, BackgroundTask] = {}
        self.task_handlers: Dict[str, Callable] = {}
        self.is_running = False
        self.stats = {
            "total_tasks": 0,
            "completed_tasks": 0,
            "failed_tasks": 0,
            "average_processing_time": 0.0
        }
        
        # Register default task handlers
        self._register_default_handlers()
    
    def _register_default_handlers(self):
        """Register default task handlers"""
        self.register_handler("conversation_analysis", self._handle_conversation_analysis)
        self.register_handler("document_processing", self._handle_document_processing)
        self.register_handler("cost_optimization", self._handle_cost_optimization)
        self.register_handler("cleanup_tasks", self._handle_cleanup_tasks)
        self.register_handler("analytics_generation", self._handle_analytics_generation)
    
    def register_handler(self, task_type: str, handler: Callable):
        """Register a task handler for a specific task type"""
        self.task_handlers[task_type] = handler
        logger.info(f"Registered handler for task type: {task_type}")
    
    def start(self):
        """Start the background agent manager"""
        if self.is_running:
            logger.warning("Background agent manager is already running")
            return
        
        self.is_running = True
        self.workers = []
        
        # Start worker threads
        for i in range(self.max_workers):
            worker = threading.Thread(target=self._worker_loop, name=f"BackgroundWorker-{i}")
            worker.daemon = True
            worker.start()
            self.workers.append(worker)
        
        logger.info(f"Started background agent manager with {self.max_workers} workers")
    
    def stop(self):
        """Stop the background agent manager"""
        self.is_running = False
        logger.info("Stopping background agent manager...")
    
    def submit_task(self, task_type: str, payload: Dict[str, Any], 
                   priority: TaskPriority = TaskPriority.MEDIUM,
                   max_retries: int = 3,
                   timeout_seconds: int = 300) -> str:
        """Submit a task for background processing"""
        task_id = f"{task_type}_{int(time.time() * 1000)}"
        
        task = BackgroundTask(
            task_id=task_id,
            task_type=task_type,
            priority=priority,
            payload=payload,
            created_at=datetime.utcnow(),
            max_retries=max_retries,
            timeout_seconds=timeout_seconds
        )
        
        self.task_queue.put(task)
        self.stats["total_tasks"] += 1
        
        logger.info(f"Submitted task {task_id} with priority {priority.name}")
        return task_id
    
    def get_task_status(self, task_id: str) -> Optional[BackgroundTask]:
        """Get the status of a specific task"""
        if task_id in self.running_tasks:
            return self.running_tasks[task_id]
        elif task_id in self.completed_tasks:
            return self.completed_tasks[task_id]
        return None
    
    def get_task_result(self, task_id: str) -> Optional[Any]:
        """Get the result of a completed task"""
        task = self.get_task_status(task_id)
        if task and task.status == TaskStatus.COMPLETED:
            return task.result
        return None
    
    def cancel_task(self, task_id: str) -> bool:
        """Cancel a pending or running task"""
        if task_id in self.running_tasks:
            task = self.running_tasks[task_id]
            task.status = TaskStatus.CANCELLED
            task.completed_at = datetime.utcnow()
            self.completed_tasks[task_id] = task
            del self.running_tasks[task_id]
            logger.info(f"Cancelled task {task_id}")
            return True
        return False
    
    def _worker_loop(self):
        """Main worker loop for processing tasks"""
        while self.is_running:
            try:
                # Get task from queue with timeout
                task = self.task_queue.get(timeout=1.0)
                
                # Process the task
                self._process_task(task)
                
            except Empty:
                # No tasks available, continue
                continue
            except Exception as e:
                logger.error(f"Error in worker loop: {e}")
                time.sleep(1)
    
    def _process_task(self, task: BackgroundTask):
        """Process a single task"""
        task.status = TaskStatus.RUNNING
        task.started_at = datetime.utcnow()
        self.running_tasks[task.task_id] = task
        
        try:
            # Get handler for task type
            handler = self.task_handlers.get(task.task_type)
            if not handler:
                raise ValueError(f"No handler registered for task type: {task.task_type}")
            
            # Execute task with timeout
            result = asyncio.run(self._execute_with_timeout(handler, task.payload, task.timeout_seconds))
            
            # Task completed successfully
            task.status = TaskStatus.COMPLETED
            task.result = result
            task.completed_at = datetime.utcnow()
            
            # Update stats
            self.stats["completed_tasks"] += 1
            processing_time = (task.completed_at - task.started_at).total_seconds()
            self._update_average_processing_time(processing_time)
            
            logger.info(f"Completed task {task.task_id} in {processing_time:.2f} seconds")
            
        except asyncio.TimeoutError:
            task.status = TaskStatus.FAILED
            task.error = f"Task timed out after {task.timeout_seconds} seconds"
            self.stats["failed_tasks"] += 1
            logger.error(f"Task {task.task_id} timed out")
            
        except Exception as e:
            task.status = TaskStatus.FAILED
            task.error = str(e)
            self.stats["failed_tasks"] += 1
            logger.error(f"Task {task.task_id} failed: {e}")
            
            # Retry if retries remaining
            if task.retry_count < task.max_retries:
                task.retry_count += 1
                task.status = TaskStatus.PENDING
                task.started_at = None
                task.error = None
                self.task_queue.put(task)
                logger.info(f"Retrying task {task.task_id} (attempt {task.retry_count + 1})")
                return
        
        finally:
            # Move task to completed tasks
            if task.task_id in self.running_tasks:
                del self.running_tasks[task.task_id]
            self.completed_tasks[task.task_id] = task
    
    async def _execute_with_timeout(self, handler: Callable, payload: Dict[str, Any], timeout: int) -> Any:
        """Execute handler with timeout"""
        return await asyncio.wait_for(
            asyncio.create_task(handler(payload)),
            timeout=timeout
        )
    
    def _update_average_processing_time(self, processing_time: float):
        """Update average processing time"""
        total_completed = self.stats["completed_tasks"]
        if total_completed == 1:
            self.stats["average_processing_time"] = processing_time
        else:
            current_avg = self.stats["average_processing_time"]
            self.stats["average_processing_time"] = (
                (current_avg * (total_completed - 1) + processing_time) / total_completed
            )
    
    def get_stats(self) -> Dict[str, Any]:
        """Get background agent statistics"""
        return {
            **self.stats,
            "running_tasks": len(self.running_tasks),
            "pending_tasks": self.task_queue.qsize(),
            "completed_tasks_count": len(self.completed_tasks),
            "is_running": self.is_running,
            "worker_count": len(self.workers)
        }
    
    def cleanup_old_tasks(self, max_age_hours: int = 24):
        """Clean up old completed tasks"""
        cutoff_time = datetime.utcnow() - timedelta(hours=max_age_hours)
        old_tasks = [
            task_id for task_id, task in self.completed_tasks.items()
            if task.completed_at and task.completed_at < cutoff_time
        ]
        
        for task_id in old_tasks:
            del self.completed_tasks[task_id]
        
        logger.info(f"Cleaned up {len(old_tasks)} old tasks")
        return len(old_tasks)
    
    # Default task handlers
    async def _handle_conversation_analysis(self, payload: Dict[str, Any]) -> Dict[str, Any]:
        """Handle conversation analysis tasks"""
        conversation_id = payload.get("conversation_id")
        messages = payload.get("messages", [])
        
        # Simulate conversation analysis
        await asyncio.sleep(2)  # Simulate processing time
        
        return {
            "conversation_id": conversation_id,
            "analysis_completed": True,
            "message_count": len(messages),
            "analysis_timestamp": datetime.utcnow().isoformat()
        }
    
    async def _handle_document_processing(self, payload: Dict[str, Any]) -> Dict[str, Any]:
        """Handle document processing tasks"""
        document_id = payload.get("document_id")
        document_type = payload.get("document_type", "unknown")
        
        # Simulate document processing
        await asyncio.sleep(5)  # Simulate processing time
        
        return {
            "document_id": document_id,
            "document_type": document_type,
            "processing_completed": True,
            "processing_timestamp": datetime.utcnow().isoformat()
        }
    
    async def _handle_cost_optimization(self, payload: Dict[str, Any]) -> Dict[str, Any]:
        """Handle cost optimization tasks"""
        optimization_type = payload.get("optimization_type", "general")
        
        # Simulate cost optimization analysis
        await asyncio.sleep(3)  # Simulate processing time
        
        return {
            "optimization_type": optimization_type,
            "optimization_completed": True,
            "recommendations": [
                "Use GPT-3.5-turbo for simple tasks",
                "Implement more aggressive caching",
                "Batch similar requests"
            ],
            "optimization_timestamp": datetime.utcnow().isoformat()
        }
    
    async def _handle_cleanup_tasks(self, payload: Dict[str, Any]) -> Dict[str, Any]:
        """Handle cleanup tasks"""
        cleanup_type = payload.get("cleanup_type", "general")
        
        # Simulate cleanup
        await asyncio.sleep(1)  # Simulate processing time
        
        return {
            "cleanup_type": cleanup_type,
            "cleanup_completed": True,
            "cleanup_timestamp": datetime.utcnow().isoformat()
        }
    
    async def _handle_analytics_generation(self, payload: Dict[str, Any]) -> Dict[str, Any]:
        """Handle analytics generation tasks"""
        analytics_type = payload.get("analytics_type", "usage")
        
        # Simulate analytics generation
        await asyncio.sleep(4)  # Simulate processing time
        
        return {
            "analytics_type": analytics_type,
            "analytics_completed": True,
            "analytics_timestamp": datetime.utcnow().isoformat(),
            "generated_reports": ["usage_report", "cost_analysis", "performance_metrics"]
        }

# Global background agent manager instance
background_agent_manager = BackgroundAgentManager(max_workers=5)

# Start the background agent manager when module is imported
background_agent_manager.start()
