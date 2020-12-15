#include "bst.h"
#include <stack>

bst::bst()
{
	root = nullptr;
}

void bst::Insert(int idIn, int ageIn, std::string nameIn)
{
	//First element in the BST
	if (root == nullptr)
	{
		root = new Node(idIn, ageIn, nameIn, nullptr);
		return;
	}

	Node * curNode = root;
	Node * targetNode = root;
	bool movedLeft = false;
	while (targetNode != nullptr)
	{
		if (idIn < curNode->id)
		{
			targetNode = curNode->leftChild;
			if (targetNode != nullptr) curNode = targetNode;
			movedLeft = true;
			continue;
		}

		if (idIn > curNode->id)
		{
			targetNode = curNode->rightChild;
			if (targetNode != nullptr) curNode = targetNode;
			movedLeft = false;
			continue;
		}

		if (idIn == curNode->id)
		{
			std::cout << "ID number " << idIn << " is already in use." << std::endl;
			return;
		}
	}

	//Target node is selecting the proper position, curNode is what points to the new node
	targetNode = new Node(idIn, ageIn, nameIn, curNode);
	if (movedLeft) curNode->leftChild = targetNode;
	else curNode->rightChild = targetNode;

	Balance(curNode);
}

void bst::Delete(int idIn)
{
	Node * deleteNode = Find(idIn);
	if (deleteNode == nullptr)
	{
		std::cout << "Node does not exist." << std::endl;
		return;
	}

	Node * leftFinal = deleteNode->leftChild;
	Node * rightFinal = deleteNode->rightChild;
	Node * parentFinal = deleteNode->parent;

	//No Children
	if (deleteNode->leftChild == nullptr && deleteNode->rightChild == nullptr)
	{
		if (parentFinal != nullptr)
		{
			if (deleteNode->parent->leftChild == deleteNode)
				deleteNode->parent->leftChild = nullptr;
			else
				deleteNode->parent->rightChild = nullptr;
		} 
		else 
			root = nullptr;
		
		delete(deleteNode);
		return;
	}

	//Only left child
	if (deleteNode->leftChild != nullptr && deleteNode->rightChild == nullptr)
	{
		if (parentFinal != nullptr)
		{
			if (deleteNode->parent->leftChild == deleteNode)
				deleteNode->parent->leftChild = deleteNode->leftChild;
			else
				deleteNode->parent->rightChild = deleteNode->leftChild;

			deleteNode->leftChild->parent = deleteNode->parent;
		}
		else
			root = deleteNode->leftChild;

		delete(deleteNode);
		return;
	}

	//Only right child
	if (deleteNode->leftChild == nullptr && deleteNode->rightChild != nullptr)
	{
		if (parentFinal != nullptr)
		{
			if (deleteNode->parent->leftChild == deleteNode)
				deleteNode->parent->leftChild = deleteNode->rightChild;
			else
				deleteNode->parent->rightChild = deleteNode->rightChild;

			deleteNode->rightChild->parent = deleteNode->parent;
		}
		else
			root = deleteNode->rightChild;

		delete(deleteNode);
		return;
	}

	//Two children
	if (deleteNode->rightChild != nullptr && deleteNode->leftChild != nullptr)
	{
		//Start on the right branch, then find the smallest node
		Node * tempNode = deleteNode->rightChild;
		while (tempNode->leftChild != nullptr)
		{
			tempNode = tempNode->leftChild;
		}

		//if deleteNode is tempNode's parent
		if (tempNode->parent == deleteNode)
		{
			tempNode->leftChild = leftFinal;
			tempNode->parent->leftChild->parent = tempNode;
			tempNode->parent = deleteNode->parent;
			
			if (tempNode->parent == nullptr) root = tempNode;

			Balance(tempNode);

			delete(deleteNode);
			return;
		}

		//if that smallest leaf DID have a right child, push it up
		if (tempNode->rightChild != nullptr)
		{
			if (tempNode->parent->leftChild == tempNode)
				tempNode->parent->leftChild = tempNode->rightChild;
			else
				tempNode->parent->rightChild = tempNode->rightChild;
			
			tempNode->rightChild->parent = tempNode->parent;
		}

		tempNode->leftChild = leftFinal;
		tempNode->leftChild->parent = tempNode;
		tempNode->rightChild = rightFinal;
		tempNode->rightChild->parent = tempNode;
		tempNode->parent = parentFinal;

		if (tempNode->parent == nullptr) root = tempNode;

		Balance(tempNode);

		delete(deleteNode);
		return;
	}
}

Node * bst::Find(int idIn)
{
	Node * returnNode = root;
	while (returnNode != nullptr)
	{
		if (idIn < returnNode->id)
		{
			returnNode = returnNode->leftChild;
			continue;
		}

		if (idIn > returnNode->id)
		{
			returnNode = returnNode->rightChild;
			continue;
		}

		if (idIn == returnNode->id)
		{
			return returnNode;
		}
	}
	return nullptr;
}

void bst::Report()
{
	std::stack<Node*> myStack;
	Node * tempNode = root;
	
	while (tempNode != nullptr || !myStack.empty())
	{
		while (tempNode != nullptr)
		{
			myStack.push(tempNode);
			tempNode = tempNode->leftChild;
		}

		tempNode = myStack.top();
		myStack.pop();

		tempNode->PrintWithLevel();
		std::cout << std::endl;
			
		tempNode = tempNode->rightChild;
	}

}

void bst::Balance(Node* curNode)
{
	//BALANCING AVL TREE~~~~~~~~~~~~~~~~~~~~~
	//Go back up to the root, check each node for imbalances
	while (curNode != nullptr)
	{
		int myBalance = curNode->BalanceFactor();
		if (myBalance >= 2)
		{
			//Left
			if (curNode->leftChild->rightChild != nullptr)
			{
				//Left Right
				if (curNode->leftChild->rightChild->leftChild != nullptr)
				curNode->leftChild->rightChild->leftChild->parent = curNode->leftChild;

				curNode->leftChild->rightChild->parent = curNode;
				curNode->leftChild->parent = curNode->leftChild->rightChild;
				curNode->leftChild->rightChild = curNode->leftChild->rightChild->leftChild;
				curNode->leftChild->parent->leftChild = curNode->leftChild;
				curNode->leftChild = curNode->leftChild->parent;
			}

			//Left Left
			if (curNode->parent != nullptr)
			{
				if (curNode->parent->leftChild == curNode) curNode->parent->leftChild = curNode->leftChild;
				else curNode->parent->rightChild = curNode->leftChild;
			}

			curNode->leftChild->parent = curNode->parent;
			curNode->parent = curNode->leftChild;
			curNode->leftChild = curNode->leftChild->rightChild;

			if (curNode->leftChild != nullptr) curNode->leftChild->parent = curNode;
			curNode->parent->rightChild = curNode;
		}
		//Right
		else
			if (myBalance <= -2)
			{
				if (curNode->rightChild->leftChild != nullptr)
				{
					//Right Left
					if (curNode->rightChild->leftChild->rightChild != nullptr)
					curNode->rightChild->leftChild->rightChild->parent = curNode->rightChild;

					curNode->rightChild->leftChild->parent = curNode;
					curNode->rightChild->parent = curNode->rightChild->leftChild;
					curNode->rightChild->leftChild = curNode->rightChild->leftChild->rightChild;
					curNode->rightChild->parent->rightChild = curNode->rightChild;
					curNode->rightChild = curNode->rightChild->parent;
				}

				//Right Right
				if (curNode->parent != nullptr)
				{
					if (curNode->parent->leftChild == curNode) curNode->parent->leftChild = curNode->rightChild;
					else curNode->parent->rightChild = curNode->rightChild;
				}

				curNode->rightChild->parent = curNode->parent;
				curNode->parent = curNode->rightChild;
				curNode->rightChild = curNode->rightChild->leftChild;
				if (curNode->rightChild != nullptr) curNode->rightChild->parent = curNode;
				curNode->parent->leftChild = curNode;
			}

		if (curNode == root && curNode->parent != nullptr) root = curNode->parent;
		curNode = curNode->parent;
	}
}
	
